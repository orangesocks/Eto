using Eto.Forms;
using swc = System.Windows.Controls;
using sw = System.Windows;
using swd = System.Windows.Data;
using swm = System.Windows.Media;
using swi = System.Windows.Input;
using Eto.Wpf.Drawing;
using Eto.Drawing;
using System.Collections.Generic;
using System;
using System.Windows;
using System.Windows.Input;
using Eto.Wpf.Forms.Controls;

namespace Eto.Wpf.Forms.Cells
{
	public class CustomCellHandler : CellHandler<swc.DataGridColumn, CustomCell, CustomCell.ICallback>, CustomCell.IHandler
	{
		public static int ImageSize = 16;

		object GetValue(object dataItem)
		{
			return dataItem;
		}

		public class WpfCellEventArgs : CellEventArgs
		{
			swc.DataGridColumn _gridColumn;
			int? _column;
			public WpfCellEventArgs(Grid grid, CustomCell cell, int row, swc.DataGridColumn column, object item, CellStates cellState, Control control = null)
				: base(grid, cell, row, -1, item, cellState, control)
			{
				_gridColumn = column;
			}

			public override int Column => _column ?? (_column = GetColumnIndex()).Value;

			int GetColumnIndex()
			{
				if (_gridColumn != null && Grid.ControlObject is swc.DataGrid grid)
				{
					return grid.Columns.IndexOf(_gridColumn);
				}
				return -1;
			}

			public void SetSelected(swc.DataGridCell cell)
			{
				var grid = cell.GetVisualParent<swc.DataGrid>();
				var selected = cell.IsSelected;
				IsSelected = selected;
				var focused = grid?.IsKeyboardFocusWithin == true;
				CellTextColor = selected ? Eto.Drawing.SystemColors.HighlightText : Eto.Drawing.SystemColors.ControlText;
			}
			public void SetRow(sw.FrameworkElement element)
			{
				SetRow(element.GetVisualParent<swc.DataGridRow>()?.GetIndex() ?? -1);
			}

			public void SetRow(int row)
			{
				Row = row;
			}

			public void SetIsEditing(bool isEditing)
			{
				IsEditing = isEditing;
			}

			public void SetDataContext(object dataContext)
			{
				Item = dataContext;
			}
		}

		public class EtoBorder : swc.Border
		{
			public Control Control { get; set; }

			public Column Column { get; set; }

			public string Identifier { get; set; }

			protected override void OnRender(swm.DrawingContext dc)
			{
				var handler = Column.Handler;
			}
		}

		public class Column : swc.DataGridColumn
		{
			public CustomCellHandler Handler { get; set; }
			static string defaultId = Guid.NewGuid().ToString();
			Dictionary<string, Stack<Control>> cellCache = new Dictionary<string,Stack<Control>>();

			Stack<Control> GetCached(string id)
			{
				id = id ?? defaultId;
				Stack<Control> cachedList;
				if (!cellCache.TryGetValue(id, out cachedList))
				{
					cachedList = new Stack<Control>();
					cellCache.Add(id, cachedList);
				}
				return cachedList;
			}

			static sw.DependencyProperty dpSelectedHookedUp = sw.DependencyProperty.Register("SelectedHandled", typeof(bool), typeof(sw.FrameworkElement));

			EtoBorder Create(swc.DataGridCell cell)
			{
				var control = GetControl<EtoBorder>(cell);
				if (control == null)
				{
					control = new EtoBorder { Column = this };
					control.Unloaded += HandleControlUnloaded;
					control.Loaded += HandleControlLoaded;
					control.DataContextChanged += HandleControlDataContextChanged;
					control.PreviewMouseDown += HandlePreviewMouseDown;
					control.IsKeyboardFocusWithinChanged += HandleIsKeyboardFocusWithinChanged;

					if (!Equals(cell.GetValue(dpSelectedHookedUp), true))
					{
						cell.SetValue(dpSelectedHookedUp, true);
						cell.Selected += HandleCellSelectedChanged;
						cell.Unselected += HandleCellSelectedChanged;
					}
					var grid = cell.GetVisualParent<swc.DataGrid>();
					if (grid != null && !Equals(grid.GetValue(dpSelectedHookedUp), true))
					{
						grid.SetValue(dpSelectedHookedUp, true);
						grid.IsKeyboardFocusWithinChanged += HandleRowFocusChanged;
					}
				}
				return control;
			}

			private void HandlePreviewMouseDown(object sender, MouseButtonEventArgs e)
			{
				var ctl = sender as sw.FrameworkElement;
				var cell = ctl?.GetVisualParent<swc.DataGridCell>();
				if (!cell.IsKeyboardFocusWithin)
				{
					cell.IsEditing = true;
					//var row = cell.GetVisualParent<swc.DataGridRow>();
					//if (row != null)
					//	row.IsSelected = true;
					//var ee = new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, e.ChangedButton, e.StylusDevice);
					//ee.RoutedEvent = sw.FrameworkElement.MouseDownEvent;
					//cell.RaiseEvent(ee);
					//e.Handled = true;
				}
			}

			void HandleIsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
			{
				var h = Handler;
				var ctl = sender as sw.FrameworkElement;
				var cell = ctl?.GetVisualParent<swc.DataGridCell>();
				var isEditing = ctl.IsKeyboardFocusWithin || cell.IsEditing;
				var args = GetEditArgs(cell, ctl);
				if (args?.IsEditing != isEditing)
				{
					args.SetIsEditing(isEditing);
					//cell.IsEditing = isEditing;
					if (isEditing)
					{
						h.Callback.OnBeginEdit(h.Widget, args);
					}
					else {
						h.Callback.OnCommitEdit(h.Widget, args);
						h.ContainerHandler.CellEdited(h, ctl);
					}
				}
			}
			static void HandleRowFocusChanged(object sender, sw.DependencyPropertyChangedEventArgs e)
			{
				var grid = sender as swc.DataGrid;
				if (grid == null)
					return;
				foreach (var item in grid.SelectedItems)
				{
					var row = grid.ItemContainerGenerator.ContainerFromItem(item) as swc.DataGridRow;
					if (row == null)
						continue;
					foreach (var ctl in row.FindVisualChildren<swc.DataGridCell>())
					{
						HandleCellSelectedChanged(ctl, null);
					}
				}
			}

			static readonly object CellEventArgs_Key = new object();

			static void HandleControlDataContextChanged(object sender, sw.DependencyPropertyChangedEventArgs e)
			{
				var ctl = sender as EtoBorder;
				var cell = ctl?.GetParent<swc.DataGridCell>();
				var col = cell?.Column as Column;
				var handler = col?.Handler;
				if (handler == null)
					return;
				var args = new WpfCellEventArgs(handler.ContainerHandler?.Grid, handler.Widget, -1, cell.Column, ctl.DataContext, CellStates.None);
				args.SetSelected(cell);
				args.SetRow(cell);
				var id = handler.Callback.OnGetIdentifier(handler.Widget, args);
				var child = ctl.Control;
				if (id != ctl.Identifier || child == null)
				{
					Stack<Control> cache;
					if (child != null)
					{
						// store old child into cache
						cache = col.GetCached(ctl.Identifier);
						cache.Push(child);
					}
					// get new from cache or create if none created yet
					cache = col.GetCached(id);
					if (cache.Count > 0)
					{
						child = cache.Pop();
						if (child.Properties.ContainsKey(CellEventArgs_Key))
						{
							args = child.Properties.Get<WpfCellEventArgs>(CellEventArgs_Key);
							args.SetSelected(cell);
							args.SetDataContext(ctl.DataContext);
						}
						else
							child.Properties.Set(CellEventArgs_Key, args);
					}
					else
					{
						child = handler.Callback.OnCreateCell(handler.Widget, args);
						child?.Properties.Set(CellEventArgs_Key, args);
					}
					if (ctl.IsLoaded && child?.Loaded == false)
					{
						child.GetWpfFrameworkElement()?.SetScale(true, true);
						child.AttachNative();
					}
					ctl.Control = child;
					ctl.Identifier = id;
					ctl.Child = child.ToNative();
				}
				else
				{
					if (child.Properties.ContainsKey(CellEventArgs_Key))
					{
						args = child.Properties.Get<WpfCellEventArgs>(CellEventArgs_Key);
						args.SetSelected(cell);
						args.SetDataContext(ctl.DataContext);
					}
					else
						child.Properties.Set(CellEventArgs_Key, args);
				}
				handler.Callback.OnConfigureCell(handler.Widget, args, child);

				handler.FormatCell(ctl, cell, ctl.DataContext);
				ctl.InvalidateVisual();
			}

			static void HandleControlLoaded(object sender, sw.RoutedEventArgs e)
			{
				// WPF's loaded event is called more than once, e.g. when on a tab that is not initially visible.
				var wpfctl = sender as EtoBorder;
				var ctl = wpfctl.Control;
				if (ctl != null && !ctl.Loaded)
				{
					ctl.GetWpfFrameworkElement()?.SetScale(true, true);
					ctl.AttachNative();
				}
			}

			static void HandleControlUnloaded(object sender, sw.RoutedEventArgs e)
			{
				var wpfctl = sender as EtoBorder;
				var ctl = wpfctl.Control;
				if (ctl != null && ctl.Loaded)
					ctl.DetachNative();
			}

			static void HandleCellSelectedChanged(object sender, sw.RoutedEventArgs e)
			{
				var cell = sender as swc.DataGridCell;
				var col = cell?.Column as Column;
				var ctl = GetControl<EtoBorder>(cell);

				if (ctl != null)
				{
					var args = ctl.Control?.Properties.Get<WpfCellEventArgs>(CellEventArgs_Key);
					if (args != null)
					{
						args.SetSelected(cell);
						col.Handler.Callback.OnConfigureCell(col.Handler.Widget, args, ctl.Control);
					}
				}
			}

			protected override sw.FrameworkElement GenerateElement(swc.DataGridCell cell, object dataItem)
			{
				return Handler.SetupCell(Create(cell));
			}

			protected override sw.FrameworkElement GenerateEditingElement(swc.DataGridCell cell, object dataItem)
			{
				return Handler.SetupCell(Create(cell));
			}

			protected override object PrepareCellForEdit(sw.FrameworkElement editingElement, sw.RoutedEventArgs editingEventArgs)
			{
				var obj = base.PrepareCellForEdit(editingElement, editingEventArgs);
				var handler = Handler;
				var cell = editingElement?.GetParent<swc.DataGridCell>();
				var args = GetEditArgs(cell, editingElement);
				if (handler != null && args != null)
				{
					args.SetIsEditing(true);
					handler.Callback.OnBeginEdit(handler.Widget, args);
				}
				if (args?.Handled != true && !cell.IsKeyboardFocusWithin)
				{
					// default implementation is to focus the cell's control
					editingElement.Focus();
					editingElement.MoveFocus(new swi.TraversalRequest(swi.FocusNavigationDirection.First));
				}
				return obj;
			}

			WpfCellEventArgs GetEditArgs(swc.DataGridCell cell, FrameworkElement editingElement)
			{
				var handler = Handler;
				if (handler == null)
					return null;
				var ctl = editingElement as EtoBorder;
				var args = ctl.Control.Properties.Get<WpfCellEventArgs>(CellEventArgs_Key);
				if (args == null)
				{
					args = new WpfCellEventArgs(handler.ContainerHandler?.Grid, handler.Widget, -1, cell.Column, ctl.DataContext, CellStates.None, ctl.Control);
					ctl.Control.Properties.Set(CellEventArgs_Key, args);
				}
				args.Handled = false;
				return args;
			}

			protected override bool CommitCellEdit(sw.FrameworkElement editingElement)
			{
				var result = base.CommitCellEdit(editingElement);
				var handler = Handler;
				var cell = editingElement?.GetParent<swc.DataGridCell>();
				var args = GetEditArgs(cell, editingElement);
				if (handler != null && args != null)
				{
					args.SetIsEditing(false);
					cell.IsEditing = false;
					handler.Callback.OnCommitEdit(handler.Widget, args);
				}
				if (args?.Handled != true && editingElement.IsKeyboardFocusWithin)
				{
					// default implementation is to move focus back to the grid
					var grid = editingElement.GetVisualParent<swc.DataGrid>();
					grid?.Focus();
				}
				handler?.ContainerHandler.CellEdited(handler, editingElement);

				return true;
			}

			protected override void CancelCellEdit(sw.FrameworkElement editingElement, object uneditedValue)
			{
				var handler = Handler;
				var cell = editingElement?.GetParent<swc.DataGridCell>();
				var args = GetEditArgs(cell, editingElement);
				if (handler != null && args != null)
				{
					args.SetIsEditing(false);
					cell.IsEditing = false;
					handler.Callback.OnCancelEdit(handler.Widget, args);
				}
				if (args?.Handled != true && editingElement.IsKeyboardFocusWithin)
				{
					editingElement.GetVisualParent<swc.DataGrid>()?.Focus();
				}
			}
		}

		public CustomCellHandler()
		{
			Control = new Column { Handler = this };

		}
	}
}