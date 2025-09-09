using System.Windows;
using System.Windows.Controls;

namespace m3u8Downloader.View.Controls
{
    public partial class LabeledInput : System.Windows.Controls.UserControl
    {
        public LabeledInput()
        {
            InitializeComponent();
        }

        public string Label
        {
            get { return (string)GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register("Label", typeof(string), typeof(LabeledInput), new PropertyMetadata(string.Empty));

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(LabeledInput), new PropertyMetadata(string.Empty));


        public string PlaceHolder
        {
            get { return (string)GetValue(PlaceHolderProperty); }
            set { SetValue(PlaceHolderProperty, value); }
        }

        public static readonly DependencyProperty PlaceHolderProperty =
            DependencyProperty.Register(
                "PlaceHolder",                    // tên property
                typeof(string),                   // kiểu dữ liệu
                typeof(LabeledInput),             // owner type (control này)
                new PropertyMetadata(string.Empty) // giá trị mặc định
            );

        public double InputHeight
        {
            get { return (double)GetValue(InputHeightProperty); }
            set { SetValue(InputHeightProperty, value); }
        }

        public static readonly DependencyProperty InputHeightProperty =
            DependencyProperty.Register(
                "InputHeight",
                typeof(double),
                typeof(LabeledInput),
                new PropertyMetadata(double.NaN) // mặc định: Auto
            );


    }
}
