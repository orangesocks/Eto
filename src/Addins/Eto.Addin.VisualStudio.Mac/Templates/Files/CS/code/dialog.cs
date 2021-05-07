﻿using System;
using System.Collections.Generic;
using Eto.Forms;
using Eto.Drawing;

namespace ${Namespace}
{	
	public class ${EscapedIdentifier} : Dialog
	{	
		public ${EscapedIdentifier}()
		{
			Title = "My ${EscapedIdentifier} dialog";

			Content = new StackLayout
			{
				Items =
				{
					new Label { Text = "Some content" },
				}
			};

			// buttons
			DefaultButton = new Button { Text = "OK" };
			PositiveButtons.Add(DefaultButton);

			AbortButton = new Button { Text = "C&ancel" };
			AbortButton.Click += (sender, e) => Close();
			NegativeButtons.Add(AbortButton);
		}
	}
}
