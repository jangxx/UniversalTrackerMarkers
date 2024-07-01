using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace UniversalTrackerMarkers
{
    /// <summary>
    /// Interaction logic for LabeledSlider.xaml
    /// </summary>
    public partial class LabeledSlider : UserControl
    {
        public string LabelText
        {
            get { return (string)GetValue(LabelTextProperty); }
            set { SetValue(LabelTextProperty, value); }
        }
        public static readonly DependencyProperty LabelTextProperty =
            DependencyProperty.Register("LabelText", typeof(string), typeof(LabeledSlider), new PropertyMetadata(null));

        public int LabelWidth
        {
            get { return (int)GetValue(LabelWidthProperty); }
            set { SetValue(LabelWidthProperty, value); }
        }
        public static readonly DependencyProperty LabelWidthProperty =
            DependencyProperty.Register("LabelWidth", typeof(int), typeof(LabeledSlider), new PropertyMetadata(null));

        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(LabeledSlider), new PropertyMetadata(0.0));

        public double Min
        {
            get { return (double)GetValue(MinProperty); }
            set
            {
                SetValue(MinProperty, value);
            }
        }
        public static readonly DependencyProperty MinProperty =
            DependencyProperty.Register("Min", typeof(double), typeof(LabeledSlider), new PropertyMetadata(0.0));

        public double Max
        {
            get { return (double)GetValue(MaxProperty); }
            set
            {
                SetValue(MaxProperty, value);
            }
        }
        public static readonly DependencyProperty MaxProperty =
            DependencyProperty.Register("Max", typeof(double), typeof(LabeledSlider), new PropertyMetadata(0.0));


        public LabeledSlider()
        {
            InitializeComponent();
        }

        private void HandleInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up || e.Key == Key.Down)
            {
                double range = Max - Min;
                double step = range / 200.0;

                if (e.Key == Key.Up)
                {
                    Value += step;
                }
                else
                {
                    Value -= step;
                }

                e.Handled = true;
            }
        }
    }
}
