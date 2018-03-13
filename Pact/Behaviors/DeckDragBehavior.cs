﻿using System.Windows;
using System.Windows.Input;
using System.Windows.Interactivity;
using System.Windows.Media;

namespace Pact.Behaviors
{
    public sealed class DeckDragBehavior
        : Behavior<DeckView>
    {
        private UIElement DropHighlightBottom => ((UIElement)AssociatedObject.FindName("DropHighlightBottom"));
        private UIElement DropHighlightTop => ((UIElement)AssociatedObject.FindName("DropHighlightTop"));
        private int DeckPosition => ((DeckViewModel)AssociatedObject.DataContext).Position;

        protected override void OnAttached()
        {
            base.OnAttached();

            // Drag source event handlers:
            AssociatedObject.GiveFeedback += AssociatedObject_GiveFeedback;
            AssociatedObject.MouseMove += AssociatedObject_MouseMove;

            AssociatedObject.AllowDrop = true;

            // Drop target event handlers:
            AssociatedObject.DragEnter += AssociatedObject_DragEnter;
            AssociatedObject.DragLeave += AssociatedObject_DragLeave;
            AssociatedObject.Drop += AssociatedObject_Drop;
        }

        private void AssociatedObject_DragEnter(object sender, DragEventArgs e)
        {
            int targetPosition = DeckPosition;
            int.TryParse((string)e.Data.GetData(DataFormats.StringFormat), out int sourcePosition);

            if (sourcePosition > targetPosition)
                DropHighlightTop.Visibility = Visibility.Visible;
            else
                DropHighlightBottom.Visibility = Visibility.Visible;
        }

        private void AssociatedObject_DragLeave(object sender, DragEventArgs e)
        {
            DropHighlightTop.Visibility = Visibility.Hidden;
            DropHighlightBottom.Visibility = Visibility.Hidden;
        }

        private void AssociatedObject_Drop(object sender, DragEventArgs e)
        {
            int targetPosition = DeckPosition;
            if (int.TryParse((string)e.Data.GetData(DataFormats.StringFormat), out int sourcePosition) && sourcePosition != targetPosition)
            {
                DropHighlightTop.Visibility = Visibility.Hidden;
                DropHighlightBottom.Visibility = Visibility.Hidden;

                System.Diagnostics.Debug.WriteLine($"{sourcePosition} moving to {targetPosition}");

                ((DeckViewModel)AssociatedObject.DataContext).EmplaceDeck(sourcePosition);
            }
        }

        private void AssociatedObject_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            e.Handled = true;
        }

        private void AssociatedObject_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var originalZIndex = (int)AssociatedObject.GetValue(System.Windows.Controls.Panel.ZIndexProperty);
                DependencyObject contentPresenter = VisualTreeHelper.GetParent(AssociatedObject);
                contentPresenter.SetValue(System.Windows.Controls.Panel.ZIndexProperty, int.MaxValue);
                AssociatedObject.SetValue(UIElement.OpacityProperty, 0.5);
                AssociatedObject.SetValue(UIElement.IsHitTestVisibleProperty, false);
                Mouse.SetCursor(Cursors.SizeNS);

                DragDrop.DoDragDrop(AssociatedObject, DeckPosition.ToString(), DragDropEffects.Move);

                // Reset visual changes from the drag:
                Mouse.SetCursor(Cursors.Arrow);
                AssociatedObject.SetValue(UIElement.IsHitTestVisibleProperty, true);
                AssociatedObject.SetValue(UIElement.OpacityProperty, 1d);
                contentPresenter.SetValue(System.Windows.Controls.Panel.ZIndexProperty, originalZIndex);
            }
        }
    }
}
