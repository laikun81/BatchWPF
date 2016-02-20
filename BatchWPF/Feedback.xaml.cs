using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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

namespace BatchWPF
{
    /// <summary>
    /// Feedback.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Feedback : UserControl
    {
        public Feedback()
        {
            InitializeComponent();
        }

        public static Nullable<Boolean> PopUp(FeedEventArg e)
        {
            var close = default(Nullable<Boolean>);
            Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() =>
            {
                var pop = new Window();
                pop.SizeToContent = SizeToContent.WidthAndHeight;
                var panel = new StackPanel();
                pop.Content = panel;
                var feed = new Feedback();
                panel.Children.Add(feed);

                feed.label.Content = e.Msg;
                if (String.IsNullOrWhiteSpace(e.Feed))
                    feed.textBox.Visibility = Visibility.Hidden;
                else
                    feed.textBox.Text = e.Feed;

                feed.button_true.Click += (m, n) => { e.Feed = feed.textBox.Text; e.Bool = true; pop.Close(); };
                feed.button_false.Click += (m, n) => { e.Feed = feed.textBox.Text; e.Bool = false; pop.Close(); };

                close = pop.ShowDialog();
            }));

            while (!close.HasValue)
            {
                Thread.Sleep(100);
            }

            return close;
        }

    }

}