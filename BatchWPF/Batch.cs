using ArkWrap;
using KS;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 1. 씨지 일괄변환
/// 2. 페이지 선별 재압축
/// 
/// 현재 단계를 알리거나(일방적 메세지)
/// 현재 진행률을 전달하거나(역시나 일방적)
/// 사용자의 입력을 기다리거나(소통)
/// </summary>
namespace BatchWPF
{



    // 로그 기록 기능도 넣어야 하는데...

    // 이벤트 분류들은
    // 우선 프로그레스바 표시를위한 이벤트. 백그라운드 워커랑 연결해야 함.
    // 압축해제 라이브러리와 연결해야하는 이벤트.
    // 이벤트형식을 이용함으로 각 메서드간의 의존성을 낮추고자 함
    #region 이벤트들
    // 피드백 받아야 할 항목들이 여러개 일 경우 대비?
    // 오브젝트 자체를 받아야 할 상황도 필요함.(이미지)
    /// <summary>
    /// 메세지 전달을 위한 객체
    /// </summary>
    public class MsgEventArg : EventArgs
    {
        private readonly string m_msg = "";
        public String Msg { get { return m_msg; } }
        public MsgEventArg(string msg) { m_msg = msg; }
        public override string ToString() { return "[MSG] " + m_msg; }
    }
    /// <summary>
    ///  사용자소통을 위한 객체
    /// </summary>
    public class FeedEventArg : EventArgs
    {
        /// <summary>
        /// 피드백 안내 메세지
        /// </summary>
        private readonly string m_msg = "";
        /// <summary>
        /// Yes or No
        /// </summary>
        private bool m_bool;
        /// <summary>
        /// 피드될 사용자 입력 문자열
        /// </summary>
        private string m_feed = "";
        public String Msg { get { return m_msg; } }
        public String Feed { get { return m_feed; } set { m_feed = value; } }
        public Boolean Bool { get { return m_bool; } set { m_bool = value; } }
        public FeedEventArg() { m_bool = true; }
        public FeedEventArg(string msg) : this() { m_msg = msg; }
        public FeedEventArg(string msg, string feed) : this(msg) { m_feed = feed; }
    }
    /// <summary>
    /// 프로세스 진행률 전달을 위한 이벤트 매개변수 클래스
    /// </summary>
    public class ProgressEventArg : EventArgs
    {
        private readonly int m_max, m_cursor;
        private readonly string m_msg = "";

        public int Max { get { return m_max; } }
        public int Cursor { get { return m_cursor; } }
        public string MSG { get { return m_msg; } }

        public ProgressEventArg(int max, int cursor, string msg) : this(msg)
        {
            m_max = max;
            m_cursor = cursor;
        }

        public ProgressEventArg(string msg)
        {
            m_msg = msg;
        }

        public override string ToString()
        {
            if (m_max == 0)
                return "[Progress...] Max : " + m_max + @" / Cursor : " + m_cursor + @" / MSG : " + m_msg;
            else
                return "[Progress...] MSG : " + m_max;
        }
    }
    #endregion

    internal class BatchWork : SingletonBase<BatchWork>
    {
        #region 이벤트핸들러
        /// <summary>
        /// 현재 작업단계를 전달하는 핸들러
        /// </summary>
        internal event EventHandler<MsgEventArg> handler_msg;
        /// <summary>
        /// 현재 진행률을 전달하는 핸들러
        /// </summary>
        internal event EventHandler<ProgressEventArg> handler_progress;
        /// <summary>
        /// 피드백 핸들러
        /// </summary>
        internal event EventHandler<FeedEventArg> handler_feed;

        private void handler_exc<T>(T e, ref EventHandler<T> handler)
        {
            KS.Util.DebugMSG(e.ToString());

            EventHandler<T> temp = Volatile.Read<EventHandler<T>>(ref handler);

            if (temp != null) temp(this, e);
        }

        private void workwork(ProgressEventArg e)
        {
            KS.Util.DebugMSG(e.ToString());

            EventHandler<ProgressEventArg> temp = Volatile.Read<EventHandler<ProgressEventArg>>(ref handler_progress);

            if (temp != null) temp(this, e);
        }
        private void workwork(int max, int cursor, string msg)
        {
            workwork(new ProgressEventArg(max, cursor, msg));
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="msg"></param>
        private void workwork(string msg)
        {
            workwork(new ProgressEventArg(msg));
        }
        // ArkWork 이벤트와 연결            
        private EventHandler<ArkWorkEventArg> arkHandler;
        private void handleProgressBar(EventHandler<ArkWorkEventArg> p)
        {
            // 핸들러가 중첩되는걸 막기 위해.(1회성 사용을 위해)
            ArkWork.Instance.Progressing -= arkHandler;
            arkHandler = p;
            ArkWork.Instance.Progressing += arkHandler;
        }

        private void workFeed(FeedEventArg e)
        {
            KS.Util.DebugMSG(e.ToString());

            EventHandler<FeedEventArg> temp = Volatile.Read<EventHandler<FeedEventArg>>(ref handler_feed);

            if (temp != null) temp(this, e);
        }
        #endregion

        // 필터링. 장기적으로는 디비에 등록해야할듯...
        static string[] deltag = { "[ev only]", "[Jpg]", "[Full Rip]", "[bmp]" };
        static string[] delfilename = { "BlogAcg.info_", "BlogaAcg.info_", "girlcelly@" };
        static string[] filterfile = { "blogacg.info.jpg", "NemuAndHaruka.png", "NemuAndHaruka.jpg", "BlogAcg.info.jpg" };
        static long[] filtersize = { 69983, 86056, 407395, 1338582 };
        static string[] filterext = { "jpg", "jpeg", "png", "bmp", "tiff", "gif" };

        /// <summary>
        /// 그 어떤 작업도 결국 하나의 파일을 대상으로 하게 됨
        /// </summary>
        private string m_target = "";
        private string m_mode = "";

        internal static void Do(params string[] args)
        {
            if (args == null || args.Length < 2)
                throw new ApplicationException("매개변수이상");

            var m_mode = args[0];
            var m_target = args[1];

            if (string.IsNullOrWhiteSpace(m_mode) || string.IsNullOrWhiteSpace(m_target))
                throw new ApplicationException("매개변수이상");

            // 우선은 메인윈도우(껍데기)를 생성
            var window = new MainWindow();
            // 일괄변환이라면
            if (m_mode.Equals("HCG", StringComparison.OrdinalIgnoreCase))
            {
                window.Title = "HCG Batch Convert";
                // 프로그레스바를 생성하고 메인 윈도우에 탑재
                var control = new ProgressBar();
                window.mainPanel.Children.Add(control);

                var batch = new BatchWork();

                batch.handler_msg += (o, e) =>
                {
                    control.ReportProgress(-1, e.Msg);
                };

                batch.handler_feed += (x, y) => { var close = Feedback.PopUp(y); };

                batch.handler_progress += (o, e) =>
                {
                    control.ReportProgress((int)Math.Round(e.Max == 0 ? 0 : ((double)e.Cursor * 100) / e.Max), e.MSG);
                };

                window.Show();

                control.DoWork(() => batch.HCG(m_target));
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="archive"></param>
        /// <returns></returns>
        internal string HCG(string archive)
        {
            workwork(new ProgressEventArg(0, 0, "HCG Convert Start"));

            // 대상이 폴더이거나 파일이거나
            var target = Path.HasExtension(archive) ? new FileInfo(archive) as FileSystemInfo : new DirectoryInfo(archive) as FileSystemInfo;
            // 대상의 부모 디렉터리
            var parent = target is FileInfo ? (target as FileInfo).Directory : (target as DirectoryInfo).Parent;

            var feed = new FeedEventArg("Progress Start ");
            workFeed(feed);

            // 압축해제 준비
            if (target is DirectoryInfo)
            {
                // ev압축파일의 경우
                var ev = (target as DirectoryInfo).GetFiles("*.*", SearchOption.AllDirectories);
                ArkWork.Instance.Load(ev.Length == 1 ? ev[0].FullName : ev.First(x => x.Name.Contains("ev")).FullName);
            }
            else
                ArkWork.Instance.Load(archive);

            for (int i = 0; i < ArkWork.Instance.Count; i++)
            {
                var file = ArkWork.Instance[i];
                var name = file.FullName;
                // 확장자 변경
                file.Ext = "jpg";
                // 알려진 이름 패턴을 삭제
                file.Name = KS.Util.WordsReplaceMulti(file.Name, delfilename);
                workwork(ArkWork.Instance.Count, i, "ReName : " + name + " => " + file.FullName);
            }
            // 프로그레스바 연계
            handleProgressBar((o, e) => workwork(e.Count, e.Cursor, "Extract : " + e.Progress));
            ArkWork.Instance.Extract(x =>
            {
                if (x == null)
                    return null;
                try
                {
                    return KS.Util.ImageUtilities.ConvertJpg(x);
                }
                catch (Exception)
                {
                    return null;
                }
            });

            // 필터링
            // 폴더 제외
            ArkWork.Instance.AddFilter(x => x.IsFolder);
            // 데이터이상제외
            ArkWork.Instance.AddFilter(x => x.Data == null);
            // 파일사이즈
            ArkWork.Instance.AddFilter(x => filtersize.Contains(x.Size));
            // 파일명
            ArkWork.Instance.AddFilter(x => filterfile.Any(y => y.Equals(x.FullName, StringComparison.OrdinalIgnoreCase)));

            handleProgressBar((o, e) => workwork(e.Count, e.Cursor, "Filtering  : " + e.Progress));
            // 필터링
            ArkWork.Instance.Filtering();

            handleProgressBar((o, e) => workwork(e.Count, e.Cursor, "Create ZIp : " + e.Progress));
            var zipName = KS.Util.WordsReplaceMulti(target.Name, deltag).Trim();
            var zipPath = parent.FullName + Path.DirectorySeparatorChar + zipName;

            // 파일 이름을 사용자에 확인하는 로직 삽입
            // 하고 싶은데... 제어권을 어떻게 넘겨주지? 이벤트로.
            // 이벤트 발생 시 이벤트 핸들러 내의 로직을 완료할때 까지 일시 정지 상태가 되겠지. 정말?
            feed = new FeedEventArg(zipName);
            workFeed(feed);
            zipName = feed.Feed;
            //

            if (File.Exists(zipPath))
                zipPath += "_new";

            // 압축시작
            zipPath = ArkWork.Instance.CreateCBZ(parent.FullName, zipName);

            workwork(new ProgressEventArg(100, 100, "Complete : " + zipPath));

            //
            // 필터링으로 제외된 파일들 체크 필요???
            //

            // 압축 헬퍼클래스 종료
            ArkWork.Instance.Close();
            // 원본 타겟 파일(폴더) 휴지통으로 이동
            KS.Util.FileGoRecycle(target.FullName);

            return zipPath;
        }
    }
}

