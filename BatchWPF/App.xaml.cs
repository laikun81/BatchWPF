using System.Windows;

namespace BatchWPF
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                BatchWork.Do(e.Args);
            }
            catch (System.Exception ex)
            {
#if DEBUG
                throw ex;
#else
                Application.Current.Shutdown();                
#endif
            }
        }
    }
}
