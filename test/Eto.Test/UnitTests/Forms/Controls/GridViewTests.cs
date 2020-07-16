using System;
using NUnit.Framework;
using Eto.Forms;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Eto.Drawing;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace Eto.Test.UnitTests.Forms.Controls
{
	public abstract class GridTests<T> : TestBase
		where T: Grid, new()
	{
		class GridTestItem : TreeGridItem
		{
			public string Text { get; set; }
		}

		[Test, ManualTest]
		public void BeginEditShoudWorkOnCustomCells()
		{
			ManualForm("The custom cell should go in edit mode when clicking the BeginEdit button", form =>
			{
				var grid = new T();
				grid.ShowHeader = true;
				grid.AllowMultipleSelection = true;

				string CellInfo(GridViewCellEventArgs e) => $"Row: {e.Row}, Column: {e.Column}";
				string CellEditInfo(CellEventArgs e) => $"Row: {e.Row}";
				void AddLogging(CustomCell cell)
				{
					cell.BeginEdit += (sender, e) => Log.Write(sender, $"BeginEdit {CellEditInfo(e)}, Grid.IsEditing: {grid.IsEditing}");
					cell.CommitEdit += (sender, e) => Log.Write(sender, $"CommitEdit {CellEditInfo(e)}, Grid.IsEditing: {grid.IsEditing}");
					cell.CancelEdit += (sender, e) => Log.Write(sender, $"CancelEdit {CellEditInfo(e)}, Grid.IsEditing: {grid.IsEditing}");

					if (!CustomCell.SupportsControlView)
					{
						cell.GetPreferredWidth = args => 100;
						cell.Paint += (sender, e) => {
							e.Graphics.DrawText(SystemFonts.Default(), Brushes.Black, e.ClipRectangle, "Cell", alignment: FormattedTextAlignment.Center);
						};
					}

				}

				grid.CellEditing += (sender, e) => Log.Write(sender, $"CellEditing {CellInfo(e)}, Grid.IsEditing: {grid.IsEditing}");
				grid.CellEdited += (sender, e) => Log.Write(sender, $"CellEdited {CellInfo(e)}, Grid.IsEditing: {grid.IsEditing}");
				var customCell = new CustomCell();
				customCell.CreateCell = args =>
				{
					var textBox = new TextBox { ShowBorder = false, BackgroundColor = Colors.Transparent };

					if (!Platform.Instance.IsMac)
					{
						textBox.GotFocus += (sender, e) => textBox.BackgroundColor = SystemColors.ControlBackground;
						textBox.LostFocus += (sender, e) => textBox.BackgroundColor = Colors.Transparent;

						// ugly, there should be a better way to do this..
						var colorBinding = textBox.Bind(c => c.TextColor, args, Binding.Property((CellEventArgs a) => a.CellTextColor).Convert(c => args.IsEditing ? SystemColors.ControlText : c));
						args.PropertyChanged += (sender, e) => {
							if (e.PropertyName == nameof(CellEventArgs.IsEditing))
								colorBinding.Update();
						};
					}
					else
					{
						// macOS handles colors more automaticcally for a TextBox
					}

					textBox.TextBinding.BindDataContext((GridTestItem i) => i.Text);

					return textBox;
				};
				AddLogging(customCell);
				grid.Columns.Add(new GridColumn { DataCell = customCell, Editable = true, HeaderText = "CustomTextBox" });

				var customCell2 = new CustomCell();
				customCell2.CreateCell = args =>
				{
					var dropDown = new DropDown { Items = { "Item 1", "Item 2", "Item 3" }};

					return dropDown;
				};
				AddLogging(customCell2);
				grid.Columns.Add(new GridColumn { DataCell = customCell2, Editable = true, HeaderText = "CustomDropDown" });

				var customCell3 = new CustomCell();
				customCell3.CreateCell = args =>
				{
					var checkBox = new CheckBox();

					return checkBox;
				};
				AddLogging(customCell3);
				grid.Columns.Add(new GridColumn { DataCell = customCell3, Editable = true, HeaderText = "CustomCheckBox" });

				grid.Columns.Add(new GridColumn { DataCell = new TextBoxCell(nameof(GridTestItem.Text)), HeaderText = "TextBoxCell", Editable = true });

				var list = new List<GridTestItem>();
				list.Add(new GridTestItem { Text = "Item 1" });
				list.Add(new GridTestItem { Text = "Item 2" });
				list.Add(new GridTestItem { Text = "Item 3" });
				SetDataStore(grid, list);

				// using MouseDown so the buttons don't get focus
				var beginEditButton = new Button { Text = "BeginEdit" };
				beginEditButton.MouseDown += (sender, e) => {
					grid.BeginEdit(1, 0);
					e.Handled = true;
				};

				var commitEditButton = new Button { Text = "CommitEdit" };
				commitEditButton.MouseDown += (sender, e) => {
					grid.CommitEdit();
					e.Handled = true;
				};

				var cancelEditButton = new Button { Text = "CancelEdit" };
				cancelEditButton.MouseDown += (sender, e) => {
					grid.CancelEdit();
					e.Handled = true;
				};

				return new TableLayout(
					TableLayout.Horizontal(4, beginEditButton, commitEditButton, cancelEditButton, null),
					grid
					);
			});
		}

		protected abstract void SetDataStore(T grid, IEnumerable<object> dataStore);
	}

	[TestFixture]
	public class GridViewTests : GridTests<GridView>
	{
		protected override void SetDataStore(GridView grid, IEnumerable<object> dataStore)
		{
			grid.DataStore = dataStore;
		}

		[Test, ManualTest]
		public void CellClickShouldHaveMouseInformation()
		{
			Exception exception = null;
			Form(form =>
			{
				var label = new Label { Text = "Left click on the cell at 0, 0" };
				var gv = new GridView { Size = new Size(200, 100) };

				gv.Columns.Add(new GridColumn
				{
					DataCell = new CheckBoxCell(0),
					HeaderText = "Check"
				});
				gv.Columns.Add(new GridColumn
				{
					DataCell = new TextBoxCell(1),
					HeaderText = "Text"
				});
				gv.DataStore = new List<GridItem>
				{
					new GridItem(true, "Item 1"),
					new GridItem(false, "Item 2"),
					new GridItem(false, "Item 3")
				};

				var step = 0;


				gv.CellClick += (sender, e) =>
				{
					try
					{
						switch (step)
						{
							case 0:
								Assert.AreEqual(0, e.Column);
								Assert.AreEqual(0, e.Row);
								Assert.AreEqual(MouseButtons.Primary, e.Buttons);
								Assert.AreEqual(Keys.None, e.Modifiers);
								Assert.AreEqual(Point.Round(Mouse.Position / 4f), Point.Round(gv.PointToScreen(e.Location) / 4f));
								label.Text = "Now, left click on 1, 0";
								step = 1;
								break;
							case 1:
								Assert.AreEqual(1, e.Column);
								Assert.AreEqual(0, e.Row);
								Assert.AreEqual(MouseButtons.Primary, e.Buttons);
								Assert.AreEqual(Keys.None, e.Modifiers);
								Assert.AreEqual(Mouse.Position, gv.PointToScreen(e.Location));
								label.Text = "Now, right click on 1, 1";
								step = 2;
								break;
							case 2:
								Assert.AreEqual(1, e.Column);
								Assert.AreEqual(1, e.Row);
								Assert.AreEqual(MouseButtons.Alternate, e.Buttons);
								Assert.AreEqual(Keys.None, e.Modifiers);
								Assert.AreEqual(Mouse.Position, gv.PointToScreen(e.Location));
								label.Text = "Now, right click on 1, 2 with the shift key pressed";
								step = 3;
								break;
							case 3:
								Assert.AreEqual(1, e.Column);
								Assert.AreEqual(2, e.Row);
								Assert.AreEqual(MouseButtons.Alternate, e.Buttons);
								Assert.AreEqual(Keys.Shift, e.Modifiers);
								Assert.AreEqual(Mouse.Position, gv.PointToScreen(e.Location));
								step = 4;
								form.Close();
								break;
							default:
								Assert.Fail("Test is in an invalid state");
								break;
						}
					}
					catch (Exception ex)
					{
						exception = ex;
						form.Close();
					}
				};

				form.Content = new StackLayout
				{
					HorizontalContentAlignment = HorizontalAlignment.Stretch,
					Items = {
						label,
						gv
					}
				};
			}, -1);
			if (exception != null)
				ExceptionDispatchInfo.Capture(exception).Throw();
		}

		class MyCollection : ObservableCollection<DataItem>
		{
			public void AddRange(IEnumerable<DataItem> items)
			{
				foreach (var item in items)
					Items.Add(item);
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}
		}

		[Test, ManualTest]
		public void CollectionChangedWithResetShouldShowItems()
		{
			var count = 10;
			ManualForm($"GridView should show {count} items", form =>
			{
				var collection = new MyCollection();
				var filterCollection = new FilterCollection<DataItem>(collection);
				var myGridView = new GridView
				{
					Size = new Size(200, 260),
					DataStore = filterCollection,
					Columns = {
						new GridColumn {
							DataCell = new TextBoxCell { Binding = Eto.Forms.Binding.Property((DataItem m) => m.Id.ToString()) }
						}
					}
				};
				collection.Clear();
				collection.AddRange(Enumerable.Range(1, count).Select(r => new DataItem(r)));

				return myGridView;
			});

		}

		[Test, ManualTest]
		public void AutoSizedColumnShouldChangeSizeOfControl()
		{
			ManualForm("GridView should auto size to content", form =>
			{
				var collection = new ObservableCollection<DataItem>();
				var gridView = new GridView
				{
					Height = 180,
					DataStore = collection,
					Columns =
					{
						new GridColumn
						{
							AutoSize = true,
							DataCell = new TextBoxCell { Binding = Binding.Property((DataItem m) => m.TextValue) }
						}
					}
				};
				var item = new DataItem { TextValue = "Some Text" };
				collection.Add(item);

				var textBox = new TextBox();
				textBox.Focus();
				textBox.TextBinding.Bind(item, i => i.TextValue);
				textBox.TextChanged += (sender, e) => gridView.ReloadData(0);

				var layout = new DynamicLayout();
				layout.BeginVertical(yscale: true);
				layout.AddRow(gridView, null); // gridView is auto sized
				layout.EndVertical();

				layout.AddSeparateRow("Text:", textBox);

				return layout;
			});
		}


		class CustomCellWithTableLayout : CustomCell
		{
			protected override Control OnCreateCell(CellEventArgs args)
			{
				var label = new Label { Text = "Hello" };
				
				var button = new Button { MinimumSize = Size.Empty, Text = "..." };
				button.Bind(c => c.Visible, args, a => a.IsSelected); // kaboom when reloading!

				return new TableLayout
				{
					Rows = { new TableRow(new TableCell(label, true), button) }
				};
			}
		}

		[Test]
		public void ReloadingDataStoreShouldNotCrash()
		{
			Form f = null;
			GridView g = null;
			try
			{
				Application.Instance.Invoke(() =>
				{
					f = new Form { Size = new Size(300, 300) };

					g = new GridView();
					g.Columns.Add(new GridColumn
					{
						DataCell = new CustomCellWithTableLayout()
					});
					g.DataStore = Enumerable.Range(0, 100).Cast<object>().ToList();
					g.SelectedRow = 1;
					f.Content = g;
					f.Show();
				});

				Thread.Sleep(1000);

				Application.Instance.Invoke(() =>
				{
					g.DataStore = Enumerable.Range(0, 10).Cast<object>().ToList();
				});

				Thread.Sleep(1000);
			}
			finally
			{
				Application.Instance.Invoke(() => f?.Close());
			}
		}

		[TestCase(true, true)]
		[TestCase(true, false)]
		[TestCase(false, true)]
		[TestCase(false, false)]
		public void ClickingWithEmptyDataShouldNotCrash(bool allowEmptySelection, bool allowMultipleSelection)
		{
			Exception exception = null;
			Form(form =>
			{
				var dd = new List<GridItem>();

				dd.Add(new GridItem { Values = new[] { "Hello" } });
				var control = new GridView();
				control.AllowEmptySelection = allowEmptySelection;
				control.AllowMultipleSelection = allowMultipleSelection;
				control.Columns.Add(new GridColumn
				{
					DataCell = new TextBoxCell(0),
					Width = 100,
					HeaderText = "Text Cell"
				});
				control.DataStore = dd;
				Application.Instance.AsyncInvoke(() => {
					// can crash when had selection initially but no selection after.
					try
					{
						control.DataStore = new List<GridItem>();
					}
					catch (Exception ex)
					{
						exception = ex;
					}
					Application.Instance.AsyncInvoke(form.Close);
				});

				form.Content = control;
			});

			if (exception != null)
				ExceptionDispatchInfo.Capture(exception).Throw();
		}

		[Test, ManualTest]
		public void SettingWidthShouldDisableAutosize()
		{
			ManualForm("Width of column should be 300px and not change when scrolling",
			form => {
				var control = new GridView();
				control.Width = 400;
				control.Height = 200;
				var column = new GridColumn
				{
					DataCell = new TextBoxCell(0),
					AutoSize = true,
					Width = 300, // setting width should set AutoSize to false
					HeaderText = "Cell"
				};
				control.Columns.Add(column);

				Assert.IsFalse(column.AutoSize, "#1");

				var dd = new List<GridItem>();
				for (int i = 0; i < 1000; i++)
				{
					dd.Add(new GridItem { Values = new[] { "Row " + i } });
				}
				control.DataStore = dd;

				return control;
			});
		}
	}
}
