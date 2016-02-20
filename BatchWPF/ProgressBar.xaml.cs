using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace BatchWPF
{
    /// <summary>
    /// ProgressBar.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ProgressBar : UserControl
    {
        // 
        BackgroundWorker worker;

        public ProgressBar()
        {
            InitializeComponent();

            worker = new BackgroundWorker();

            // 백그라운드 작업에서 취소를 허용하고 진행률을 보고하게 할지 여부를 지정
            worker.WorkerSupportsCancellation = true;
            worker.WorkerReportsProgress = true;

            button_cancel.Click += (o, e) => { worker.CancelAsync(); };

            // ProgressChanged 이벤트 처리기에서 사용자 인터페이스 업데이트 등의 진행률을 나타내는 코드를 추가
            worker.ProgressChanged += (o, e) =>
            {
                var arg = e as ProgressChangedEventArgs;
                // 프로그레스바 업데이트
                if (arg.ProgressPercentage < 0)
                    label_progress.Content = arg.UserState;
                else
                {
                    progressBar.Value = arg.ProgressPercentage;
                    label_work.Content = arg.UserState;
                }
            };

            // RunWorkerCompleted 이벤트는 백그라운드 작업자가 완료되면 발생
            worker.RunWorkerCompleted += (o, e) =>
            {
                // 백그라운드 작업에서 취소를 허용하는 경우 작업이 취소되었는지 확인하려면 이벤트 처리기에 전달된 RunWorkerCompletedEventArgs 개체의 Cancelled 속성을 검사
                if ((e.Cancelled == true)) { }
                // 오류가 발생했는지 확인하려면 이벤트 처리기에 전달된 RunWorkerCompletedEventArgs 개체의 Error 속성을 검사
                else if (!(e.Error == null)) { }
                else {

                }
            };
        }

        internal void DoWork(Action work)
        {
            // 백그라운드 작업자의 DoWork 이벤트에 대한 이벤트 처리기를 만듭
            worker.DoWork += (o, e) =>
            {
                work();
            };

            // 
            worker.RunWorkerAsync();
        }

        internal void ReportProgress(int percentage, object userstate)
        {
            // 호출 프로세스에 진행률을 보고하려면 ReportProgress 메서드를 호출하고 0에서 100까지의 완료율을 전달
            worker.ReportProgress(percentProgress: percentage, userState: userstate);
        }

    }
}
