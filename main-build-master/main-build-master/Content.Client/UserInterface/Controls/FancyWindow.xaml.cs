﻿using System.Numerics;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.UserInterface.Controls
{
    [GenerateTypedNameReferences]
    [Virtual]
    public partial class FancyWindow : BaseWindow
    {
        private const int DRAG_MARGIN_SIZE = 7;

        public FancyWindow()
        {
            RobustXamlLoader.Load(this);

            CloseButton.OnPressed += _ => Close();
            XamlChildren = ContentsContainer.Children;
        }

        public string? Title
        {
            get => WindowTitle.Text;
            set => WindowTitle.Text = value;
        }

        protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
        {
            var mode = DragMode.Move;

            if (Resizable)
            {
                if (relativeMousePos.Y < DRAG_MARGIN_SIZE)
                {
                    mode = DragMode.Top;
                }
                else if (relativeMousePos.Y > Size.Y - DRAG_MARGIN_SIZE)
                {
                    mode = DragMode.Bottom;
                }

                if (relativeMousePos.X < DRAG_MARGIN_SIZE)
                {
                    mode |= DragMode.Left;
                }
                else if (relativeMousePos.X > Size.X - DRAG_MARGIN_SIZE)
                {
                    mode |= DragMode.Right;
                }
            }

            return mode;
        }
    }
}