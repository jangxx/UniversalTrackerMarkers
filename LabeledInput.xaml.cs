﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace UniversalTrackerMarkers
{
    /// <summary>
    /// Interaction logic for LabeledInput.xaml
    /// </summary>
    public partial class LabeledInput : UserControl
    {
        public string LabelText
        {
            get { return (string)GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register("LabelText", typeof(string), typeof(LabeledInput), new PropertyMetadata(null));

        public string InputText
        {
            get { return (string)GetValue(InputTextProperty); }
            set { SetValue(InputTextProperty, value); }
        }
        public static readonly DependencyProperty InputTextProperty =
            DependencyProperty.Register("InputText", typeof(string), typeof(LabeledInput), new PropertyMetadata(null));

        public bool Highlighted
        {
            get { return (bool)GetValue(HighlightedProperty); }
            set { SetValue(HighlightedProperty, value); }
        }
        public static readonly DependencyProperty HighlightedProperty =
            DependencyProperty.Register("Highlighted", typeof(bool), typeof(LabeledInput), new PropertyMetadata(null));

        public int LabelWidth
        {
            get { return (int)GetValue(LabelWidthProperty); }
            set { SetValue(LabelWidthProperty, value); }
        }
        public static readonly DependencyProperty LabelWidthProperty =
            DependencyProperty.Register("LabelWidth", typeof(int), typeof(LabeledInput), new PropertyMetadata(null));

        public event TextChangedEventHandler? TextChanged;

        public LabeledInput()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == HighlightedProperty)
            {
                bool val = (bool)e.NewValue;

                if (val)
                {
                    OuterShell.Background = new SolidColorBrush(Colors.Red);
                }
                else
                {
                    OuterShell.Background = null;
                }
            }
            else
            {
                base.OnPropertyChanged(e);
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextChanged?.Invoke(this, e);
        }
    }
}
