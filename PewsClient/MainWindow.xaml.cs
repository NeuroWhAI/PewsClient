using Gdi = System.Drawing;
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
using System.Windows.Interop;
using System.Windows.Threading;
using System.Net;
using System.Globalization;
using System.IO;
using System.Diagnostics;

namespace PewsClient
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private string DataPath = "https://www.weather.go.kr/pews/data";
        private int HeadLength = 4;
        private const int MaxEqkStrLen = 60;
        private const int MaxEqkInfoLen = 120;
        private static string[] AreaNames = { "서울", "부산", "대구", "인천", "광주", "대전", "울산", "세종", "경기", "강원", "충북", "충남", "전북", "전남", "경북", "경남", "제주" };
        private static string[] MmiColors = { "#FFFFFF", "#FFFFFF", "#A0E6FF", "#92D050", "#FFFF00", "#FFC000", "#FF0000", "#A32777", "#632523", "#4C2600", "#000000" };

        //#############################################################################################

        public MainWindow()
        {
            InitializeComponent();

            UpdateEqkMmiPanel(-1);

            HideEqkInfo();
            HideEqkMmi();
            HideWarningHint();

            txtTimeSync.Text = $"Sync: {Math.Round(m_tide):F0}ms";
        }

        private bool m_simMode = false;
        private DateTime m_simEndTime = DateTime.MinValue;

        private Gdi.Brush[] m_mmiBrushes = null;
        private Gdi.Pen[] m_mmiStagePens = null;
        private Gdi.Image m_imgMap = null;
        private Gdi.Bitmap m_canvasBitmap = null;

        private Brush[] m_mmiWpfBrushes = null;
        private Brush m_redBrush = new SolidColorBrush(Color.FromRgb(0xff, 0x10, 0x00));
        private Brush m_newsHeaderBrush = new SolidColorBrush(Color.FromRgb(0xff, 0xab, 0x00));
        private Brush m_newsBackBrush = new SolidColorBrush(Color.FromRgb(0xff, 0xd5, 0x4f));
        private Brush m_notiHeaderBrush = new SolidColorBrush(Color.FromRgb(0x29, 0xb6, 0xf6));
        private Brush m_notiBackBrush = new SolidColorBrush(Color.FromRgb(0xb3, 0xe5, 0xfc));

        private Style m_newsTextStyle = null;
        private Style m_notiTextStyle = null;

        private int m_beepLevel = 0;
        private MediaPlayer m_wavBeep = new MediaPlayer();
        private MediaPlayer m_wavBeep1 = new MediaPlayer();
        private MediaPlayer m_wavBeep2 = new MediaPlayer();
        private MediaPlayer m_wavBeep3 = new MediaPlayer();
        private MediaPlayer m_wavEnd = new MediaPlayer();
        private MediaPlayer[] m_wavNormal = new MediaPlayer[11];
        private MediaPlayer[] m_wavHigh = new MediaPlayer[11];
        private MediaPlayer[] m_wavUpdate = new MediaPlayer[11];

        private Stopwatch m_tickStopwatch = new Stopwatch();
        private DispatcherTimer m_timer = new DispatcherTimer();
        private DispatcherTimer m_timerBeep = new DispatcherTimer();

        private string m_prevBinTime = string.Empty;
        private double m_tide = 1000;
        private DateTime m_nextSyncTime = DateTime.MinValue;
        private DateTime m_serverTime = DateTime.MinValue;

        private int m_prevPhase = 1;
        private string m_prevAlarmId = string.Empty;

        private bool m_stationUpdate = true;
        private List<PewsStation> m_stations = new List<PewsStation>();

        private Gdi.PointF m_epicenter = new Gdi.PointF(-100, -100);
        private float m_waveTick = 0;

        private List<int> m_intensityGrid = new List<int>();
        private bool m_updateGrid = false;

        private int m_maxMmi = 1;
        private List<List<int>> m_stnClusters = new List<List<int>>();
        private readonly int MinClusterSize = 3;

        //#############################################################################################

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
#if DEBUG
            //StartSimulation("2017000407", "20171115142931"); // 포항 5.4
            //StartSimulation("2016000291", "20160912203254"); // 경주 5.8
            StartSimulation("2019009762", "20190721110418"); // 상주 3.9
            //StartSimulation("2019003859", "20190419111643"); // 동해 4.3
#endif

            LoadResources();
            DrawCanvas();

            m_timer.Interval = TimeSpan.FromMilliseconds(10);
            m_timer.Tick += Timer_Tick;
            m_timer.Start();

            m_timerBeep.Interval = TimeSpan.FromMilliseconds(2100);
            m_timerBeep.Tick += TimerBeep_Tick;
            m_timerBeep.Start();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            m_timer.Stop();
            m_timerBeep.Stop();
        }

        private async void Timer_Tick(object sender, EventArgs e)
        {
            bool bCheckLag = false;
            m_tickStopwatch.Restart();

            m_timer.Stop();

            try
            {
                var binTime = DateTime.UtcNow.AddMilliseconds(-m_tide);
                string binTimeStr = binTime.ToString("yyyyMMddHHmmss");
                if (m_prevBinTime == binTimeStr)
                {
                    return;
                }
                m_prevBinTime = binTimeStr;

                if (m_simMode && binTime >= m_simEndTime)
                {
                    StopSimulation();
                    return;
                }

                string url = $"{DataPath}/{binTimeStr}";


                byte[] bytes = null;

                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

                    try
                    {
                        bytes = await client.DownloadDataTaskAsync(url + ".b");
                    }
                    catch (WebException)
                    {
                        txtStatus.Text = "Loading";

                        if (m_tide < 1000)
                        {
                            m_tide += 200;
                        }
                        else
                        {
                            m_tide -= 200;
                        }
                        txtTimeSync.Text = $"Sync: {Math.Round(m_tide):F0}ms";

                        return;
                    }


                    // 시간 동기화.
                    if (m_simMode)
                    {
                        txtTimeSync.Text = $"Sync: Paused";
                    }
                    else if (DateTime.UtcNow >= m_nextSyncTime)
                    {
                        m_nextSyncTime = DateTime.UtcNow + TimeSpan.FromSeconds(10.0);

                        string stStr = client.ResponseHeaders.Get("ST");
                        if (!string.IsNullOrWhiteSpace(stStr)
                            && double.TryParse(stStr, out double serverTime))
                        {
                            m_tide = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - serverTime * 1000 + 1000;
                            txtTimeSync.Text = $"Sync: {Math.Round(m_tide):F0}ms";
                        }
                    }
                }

                if (bytes != null && bytes.Length > MaxEqkStrLen)
                {
                    // 시간 표시 갱신.
                    m_serverTime = DateTime.Now.AddMilliseconds(-m_tide);
                    txtServerTime.Text = m_serverTime.ToString("yyyy-MM-dd HH:mm:ss");


                    var headerBuff = new StringBuilder();
                    for (int i = 0; i < HeadLength; ++i)
                    {
                        headerBuff.Append(ByteToBinStr(bytes[i]));
                    }
                    string header = headerBuff.ToString();

                    var bodyBuff = new StringBuilder(ByteToBinStr(bytes[0]));
                    for (int i = HeadLength; i < bytes.Length; ++i)
                    {
                        bodyBuff.Append(ByteToBinStr(bytes[i]));
                    }
                    string body = bodyBuff.ToString();


                    // 관측소 정보 업데이트 신호 확인.
                    m_stationUpdate = (m_stationUpdate || (header[0] == '1'));

                    int phase = 0;
                    if (header[1] == '0')
                    {
                        phase = 1;
                    }
                    else if (header[1] == '1' && header[2] == '0')
                    {
                        phase = 2;
                    }
                    else if (header[2] == '1')
                    {
                        phase = 3;
                    }


                    if (phase > 1)
                    {
                        var infoBytes = bytes.Skip(bytes.Length - MaxEqkStrLen).ToArray();
                        string eqkId = HandleEqk(phase, body, infoBytes);

                        ShowEqkInfo();

                        if (m_prevPhase != phase || m_updateGrid)
                        {
                            m_updateGrid = true;
                            await RequestGridData(eqkId, phase);
                        }
                    }
                    else
                    {
                        m_updateGrid = false;
                        m_intensityGrid.Clear();

                        HideEqkInfo();

                        if (m_prevPhase > 1)
                        {
                            m_wavEnd.Stop();
                            m_wavEnd.Play();
                        }
                    }

                    m_prevPhase = phase;


                    // 관측소 데이터 갱신이 필요하면.
                    if (m_stationUpdate)
                    {
                        byte[] stnBytes = null;

                        using (var client = new WebClient())
                        {
                            client.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

                            for (int retry = 0; retry <= 2; ++retry)
                            {
                                try
                                {
                                    stnBytes = await client.DownloadDataTaskAsync(url + ".s");
                                    break;
                                }
                                catch (WebException)
                                {
                                    stnBytes = null;
                                    await Task.Delay(300);
                                }
                            }
                        }

                        if (stnBytes != null && stnBytes.Length > 0)
                        {
                            bodyBuff = new StringBuilder();
                            for (int i = 0; i < stnBytes.Length; ++i)
                            {
                                bodyBuff.Append(ByteToBinStr(stnBytes[i]));
                            }

                            HandleStn(bodyBuff.ToString());
                        }
                    }


                    // 관측소 데이터가 있으면 관측소 진도 분석.
                    if (m_stations.Count > 0)
                    {
                        int maxMmi = HandleMmi(body, phase);


                        // 경고 힌트 갱신.
                        UpdateWarningHint(m_stations);


                        // 강도별 관측소 수.
                        txtHighStn.Text = m_stations.Count((stn) => (stn.Mmi >= 5)).ToString();
                        txtMidStn.Text = m_stations.Count((stn) => (stn.Mmi >= 3 && stn.Mmi <= 4)).ToString();
                        txtLowStn.Text = m_stations.Count((stn) => (stn.Mmi == 2)).ToString();


                        // 계측진도를 띄우거나 갱신할지 여부 결정.
                        bool eqkMayOccured = (maxMmi > 3);
                        if (!eqkMayOccured)
                        {
                            // 전체 최대진도와 작은 클러스터를 무시한 최대진도는 다를 수 있으므로 확인.
                            int maxClusterMmi = 0;
                            foreach (var cluster in m_stnClusters)
                            {
                                if (cluster.Count < 2)
                                {
                                    continue;
                                }

                                int clusterMmi = -1;
                                foreach (int stnIdx in cluster)
                                {
                                    if (stnIdx < 0 || stnIdx >= m_stations.Count)
                                    {
                                        // 관측소 번호에 오류가 있으므로 클러스터 무시.
                                        clusterMmi = -1;
                                        break;
                                    }

                                    var stn = m_stations[stnIdx];
                                    if (stn.Mmi >= maxMmi)
                                    {
                                        // 최대진도와 같다는 것을 확인하였으니 바로 탈출.
                                        clusterMmi = stn.Mmi;
                                        break;
                                    }
                                    else if (stn.Mmi > clusterMmi)
                                    {
                                        clusterMmi = stn.Mmi;
                                    }
                                }

                                if (clusterMmi >= maxMmi)
                                {
                                    // 최대진도와 같다는 것을 확인하였으니 바로 탈출.
                                    maxClusterMmi = clusterMmi;
                                    break;
                                }
                                else if (clusterMmi > maxClusterMmi)
                                {
                                    maxClusterMmi = clusterMmi;
                                }
                            }

                            maxMmi = maxClusterMmi;
                            if (maxClusterMmi >= 2)
                            {
                                eqkMayOccured = true;
                            }
                        }

                        // 계측진도 표시.
                        if (maxMmi > m_maxMmi)
                        {
                            m_maxMmi = maxMmi;
                            UpdateEqkMmiPanel(maxMmi);

                            // 갱신 소리 재생.
                            // 단, 지진일 확률이 인정되는 경우에만.
                            if (eqkMayOccured
                                && maxMmi >= 0 && maxMmi < m_wavUpdate.Length)
                            {
                                m_wavUpdate[maxMmi].Stop();
                                m_wavUpdate[maxMmi].Play();
                            }
                        }

                        if (eqkMayOccured)
                        {
                            ShowEqkMmi();
                        }
                        else if (phase <= 1)
                        {
                            HideEqkMmi();
                        }


                        // 진도 종합 레벨 계산.
                        double level = 0;
                        foreach (var stn in m_stations)
                        {
                            level += Earthquake.MMIToMinimumGal(stn.Mmi);
                        }

                        UpdateMmiLevel(level);
                    }
                    else
                    {
                        m_beepLevel = 0;
                        m_maxMmi = 1;
                        UpdateEqkMmiPanel(-1);
                        m_stnClusters.Clear();
                        HideWarningHint();
                    }
                }

                if (m_prevPhase > 1)
                {
                    txtStatus.Text = "Event";
                }
                else
                {
                    txtStatus.Text = "Idle";
                }


                DrawCanvas();


                bCheckLag = true;
            }
            catch (Exception err)
            {
                txtStatus.Text = "Error: " + err.Message;
            }
            finally
            {
                m_tickStopwatch.Stop();
                var elapsed = m_tickStopwatch.Elapsed;

                if (bCheckLag)
                {
                    txtTickDelay.Text = $"Lag: {elapsed.TotalMilliseconds:F0}ms";
                }

                var leftDelay = TimeSpan.FromMilliseconds(100) - elapsed;
                if (leftDelay.TotalMilliseconds > 0)
                {
                    await Task.Delay(leftDelay);
                }

                m_timer.Start();
            }
        }

        private void TimerBeep_Tick(object sender, EventArgs e)
        {
            m_wavBeep1.Stop();
            m_wavBeep2.Stop();
            m_wavBeep3.Stop();
            m_wavBeep.Stop();

            if (m_beepLevel <= 0)
            {
                // Just stop.
            }
            else if (m_beepLevel == 1)
            {
                m_wavBeep.Play();
            }
            else if (m_beepLevel == 2)
            {
                m_wavBeep1.Play();
            }
            else if (m_beepLevel == 3)
            {
                m_wavBeep2.Play();
            }
            else
            {
                m_wavBeep3.Play();
            }
        }

        private void CheckBoxPin_Changed(object sender, RoutedEventArgs e)
        {
            var box = sender as CheckBox;
            if (box.IsChecked == true)
            {
                ShowEqkInfo();
                ShowEqkMmi();
                ShowWarningHint();
            }
        }

        //#############################################################################################

        private void LoadResources()
        {
            var mmiColors = MmiColors
                .Select((color) => HexCodeToColor(color))
                .ToArray();

            m_mmiBrushes = mmiColors
                .Select((color) => new Gdi.SolidBrush(color))
                .ToArray();
            m_mmiStagePens = new Gdi.Pen[]
            {
                new Gdi.Pen(Gdi.Color.Green, 3.5f),
                new Gdi.Pen(Gdi.Color.Yellow, 3.5f),
                new Gdi.Pen(Gdi.Color.FromArgb(0xff, 0x10, 0x00), 3.5f),
            };
            m_imgMap = Gdi.Image.FromFile("res/map.png");
            m_canvasBitmap = new Gdi.Bitmap(m_imgMap.Width, m_imgMap.Height);


            m_mmiWpfBrushes = mmiColors
                .Select((color) => new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B)))
                .ToArray();


            m_newsTextStyle = new Style(typeof(TextBlock));
            m_newsTextStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.Black));
            m_notiTextStyle = new Style(typeof(TextBlock));
            m_notiTextStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.Black));


            Action<string, Action<int, MediaPlayer>> loadWavForEachMmi = (name, callback) =>
            {
                var first = new MediaPlayer();
                first.Open(new Uri($"res/{name}.mp3", UriKind.Relative));
                callback(0, first);

                for (int mmi = 1; mmi <= 10; ++mmi)
                {
                    string wavPath = $"res/{name}{mmi}.mp3";
                    if (File.Exists(wavPath))
                    {
                        var player = new MediaPlayer();
                        player.Open(new Uri(wavPath, UriKind.Relative));
                        callback(mmi, player);
                    }
                    else
                    {
                        callback(mmi, first);
                    }
                }
            };

            m_wavBeep.Open(new Uri("res/beep.mp3", UriKind.Relative));
            m_wavBeep1.Open(new Uri("res/beep1.mp3", UriKind.Relative));
            m_wavBeep2.Open(new Uri("res/beep2.mp3", UriKind.Relative));
            m_wavBeep3.Open(new Uri("res/beep3.mp3", UriKind.Relative));
            m_wavEnd.Open(new Uri("res/end.mp3", UriKind.Relative));
            loadWavForEachMmi("normal", (mmi, player) => m_wavNormal[mmi] = player);
            loadWavForEachMmi("high", (mmi, player) => m_wavHigh[mmi] = player);
            loadWavForEachMmi("update", (mmi, player) => m_wavUpdate[mmi] = player);

            m_wavBeep.Volume = 0.25;
            m_wavBeep1.Volume = 0.25;
            m_wavBeep2.Volume = 0.25;
        }

        private void DrawCanvas()
        {
            const float ClusterPadding = 8.0f;

            if (m_imgMap == null || dock.ActualWidth < 1)
            {
                return;
            }

            int width = m_imgMap.Width;
            int height = m_imgMap.Height;
            float fWidth = m_imgMap.Width;
            float fHeight = m_imgMap.Height;

            using (var sWavePen = new Gdi.Pen(Gdi.Color.FromArgb(255, 0, 0), 2.0f))
            using (var pWavePen = new Gdi.Pen(Gdi.Color.FromArgb(0, 0, 255), 2.0f))
            {
                using (var g = Gdi.Graphics.FromImage(m_canvasBitmap))
                {
                    // Background
                    g.Clear(Gdi.Color.FromArgb(211, 211, 211));

                    // Intensity
                    var mmiIterator = m_intensityGrid.GetEnumerator();
                    bool isEnd = false;
                    for (double i = 38.85; i > 33; i -= 0.05)
                    {
                        for (double j = 124.5; j < 132.05; j += 0.05)
                        {
                            if (!mmiIterator.MoveNext())
                            {
                                isEnd = true;
                                break;
                            }

                            int mmi = mmiIterator.Current;

                            if (mmi >= 0 && mmi < m_mmiBrushes.Length)
                            {
                                var brush = m_mmiBrushes[mmi];
                                float x = (float)(LonToX(j) - 4);
                                float y = (float)(LatToY(i) - 7);

                                g.FillRectangle(brush, x, y, 8, 8);
                            }
                        }

                        if (isEnd)
                        {
                            break;
                        }
                    }

                    // Map
                    g.DrawImage(m_imgMap, new Gdi.RectangleF(0, 0, m_imgMap.Width, m_imgMap.Height));

                    // Station
                    foreach (var stn in m_stations)
                    {
                        int mmi = stn.Mmi;
                        if (mmi >= 0 && mmi < m_mmiBrushes.Length)
                        {
                            var brush = m_mmiBrushes[mmi];
                            float x = (float)(LonToX(stn.Longitude) - 4);
                            float y = (float)(LatToY(stn.Latitude) - 4);

                            g.FillRectangle(brush, x, y, 10, 10);
                            g.DrawRectangle(Gdi.Pens.Black, x, y, 10, 10);
                        }
                    }

                    // Cluster
                    foreach (var cluster in m_stnClusters)
                    {
                        if (cluster.Count < MinClusterSize)
                        {
                            continue;
                        }

                        bool bInit = true;
                        float left = 0, right = 0, top = 0, bottom = 0;
                        int maxMmi = -1;

                        foreach (int stnIdx in cluster)
                        {
                            if (stnIdx < 0 || stnIdx >= m_stations.Count)
                            {
                                // 관측소 번호에 오류가 있으므로 클러스터 무시.
                                break;
                            }

                            var stn = m_stations[stnIdx];
                            float x = (float)LonToX(stn.Longitude);
                            float y = (float)LatToY(stn.Latitude);

                            if (stn.Mmi > maxMmi)
                            {
                                maxMmi = stn.Mmi;
                            }

                            if (bInit)
                            {
                                bInit = false;
                                left = x;
                                right = x;
                                top = y;
                                bottom = y;
                            }
                            else
                            {
                                if (x < left)
                                {
                                    left = x;
                                }
                                else if (x > right)
                                {
                                    right = x;
                                }

                                if (y < top)
                                {
                                    top = y;
                                }
                                else if (y > bottom)
                                {
                                    bottom = y;
                                }
                            }
                        }

                        if (maxMmi >= 0)
                        {
                            int mmiStage = 0;
                            if (maxMmi >= 5)
                            {
                                mmiStage = 2;
                            }
                            else if (maxMmi >= 3)
                            {
                                mmiStage = 1;
                            }

                            g.DrawRectangle(m_mmiStagePens[mmiStage],
                                left - ClusterPadding, top - ClusterPadding,
                                right - left + ClusterPadding * 2, bottom - top + ClusterPadding * 2);
                        }
                    }

                    // Wave
                    if (m_prevPhase > 1
                        && m_waveTick > 0.0f && m_waveTick < 2048.0f)
                    {
                        // P
                        g.DrawEllipse(pWavePen, m_epicenter.X - m_waveTick * 2, m_epicenter.Y - m_waveTick * 2,
                            m_waveTick * 4, m_waveTick * 4);

                        // S
                        g.DrawEllipse(sWavePen, m_epicenter.X - m_waveTick, m_epicenter.Y - m_waveTick,
                            m_waveTick * 2, m_waveTick * 2);
                    }

                    // Epicenter
                    if (m_prevPhase > 1
                        && m_epicenter.X > -32 && m_epicenter.X < fWidth + 32
                        && m_epicenter.Y > -32 && m_epicenter.Y < fHeight + 32)
                    {
                        g.FillEllipse(Gdi.Brushes.Blue, m_epicenter.X - 4, m_epicenter.Y - 4, 8, 8);
                        g.DrawEllipse(Gdi.Pens.Blue, m_epicenter.X - 8, m_epicenter.Y - 8, 16, 16);
                        g.DrawEllipse(Gdi.Pens.Blue, m_epicenter.X - 12, m_epicenter.Y - 12, 24, 24);
                    }
                }

                var hbmp = m_canvasBitmap.GetHbitmap();
                try
                {
                    var options = BitmapSizeOptions.FromEmptyOptions();
                    canvas.Source = Imaging.CreateBitmapSourceFromHBitmap(hbmp, IntPtr.Zero, Int32Rect.Empty, options);
                }
                finally
                {
                    Api.DeleteObject(hbmp);
                }
            }

            canvas.InvalidateVisual();
        }

        private void StartSimulation(string eqkId, string eqkStartTime)
        {
            var startTime = DateTime.ParseExact(eqkStartTime, "yyyyMMddHHmmss", CultureInfo.InvariantCulture);

            m_simMode = true;
            m_simEndTime = startTime.AddHours(-9) + TimeSpan.FromSeconds(300);

            HeadLength = 1;
            DataPath += $"/{eqkId}";
            m_tide = (DateTime.UtcNow.AddHours(9) - startTime).TotalMilliseconds;
        }

        private void StopSimulation()
        {
            m_simMode = false;

            HeadLength = 4;
            DataPath = "https://www.weather.go.kr/pews/data";
            m_tide = 1000;

            m_stationUpdate = true;
        }

        private void ShowEqkInfo()
        {
            boxEqkInfo.Visibility = Visibility.Visible;
        }

        private void HideEqkInfo()
        {
            if (chkPin.IsChecked != true)
            {
                boxEqkInfo.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateEqkInfo(int phase, DateTimeOffset time, string location, int mmi, double magnitude, double depth = double.NaN)
        {
            Style style;

            if (phase < 3)
            {
                // 속보.

                boxEqkInfoHeader.Background = m_newsHeaderBrush;
                boxEqkInfo.Background = m_newsBackBrush;

                style = m_newsTextStyle;
            }
            else
            {
                // 통보.

                boxEqkInfoHeader.Background = m_notiHeaderBrush;
                boxEqkInfo.Background = m_notiBackBrush;

                style = m_notiTextStyle;
            }

            boxEqkInfo.Resources.Remove(typeof(TextBlock));
            boxEqkInfo.Resources.Add(typeof(TextBlock), style);

            txtEqkNotiKind.Text = (phase < 3) ? "신속정보" : "상세정보";
            txtEqkDate.Text = time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            txtEqkTime.Text = time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

            txtEqkLoc.Text = location.Trim();
            txtMmi.Text = Earthquake.MMIToString(mmi);
            txtMag.Text = magnitude.ToString("F1");
            txtDepth.Text = (double.IsNaN(depth) ? "-" : depth.ToString("F0"));
        }

        private void ShowEqkMmi()
        {
            boxEqkMmi.Visibility = Visibility.Visible;
        }

        private void HideEqkMmi()
        {
            if (chkPin.IsChecked != true)
            {
                boxEqkMmi.Visibility = Visibility.Collapsed;
            }

            UpdateEqkMmiPanel(-1);

            m_maxMmi = 1; // 다음에 계측진도가 처음부터 갱신될 수 있도록 초기화.
        }

        private void UpdateEqkMmiPanel(int mmi)
        {
            if (mmi <= 0)
            {
                boxEqkMmi.Background = Brushes.SlateGray;

                txtEqkMmi.Text = string.Empty;
                txtEqkMmi.Foreground = Brushes.Black;

                return;
            }
            else if (mmi >= MmiColors.Length)
            {
                mmi = MmiColors.Length - 1;
            }

            boxEqkMmi.Background = m_mmiWpfBrushes[mmi];

            txtEqkMmi.Text = Earthquake.MMIToString(mmi);
            txtEqkMmi.Foreground = (mmi >= 6) ? Brushes.White : Brushes.Black;
        }

        private void UpdateMmiLevel(double level)
        {
            double scale = m_stations.Count / 150.0;
            Brush lvlBrush;

            if (level < Earthquake.MMIToMinimumGal(2) * scale)
            {
                m_beepLevel = 0;
                lvlBrush = Brushes.White;
            }
            else if (level < Earthquake.MMIToMinimumGal(3) * scale)
            {
                m_beepLevel = 1;
                lvlBrush = Brushes.YellowGreen;
            }
            else if (level < Earthquake.MMIToMinimumGal(5) * scale)
            {
                m_beepLevel = 2;
                lvlBrush = Brushes.Yellow;
            }
            else if (level < Earthquake.MMIToMinimumGal(7) * scale)
            {
                m_beepLevel = 3;
                lvlBrush = Brushes.Orange;
            }
            else
            {
                m_beepLevel = 4;
                lvlBrush = m_redBrush;
            }

            lblLevel.Foreground = lvlBrush;
            txtLevel.Foreground = lvlBrush;
            txtLevel.Text = Math.Ceiling(level).ToString("F0");
        }

        private void ShowWarningHint()
        {
            boxWarningHint.Visibility = Visibility.Visible;
        }

        private void HideWarningHint()
        {
            if (chkPin.IsChecked != true)
            {
                boxWarningHint.Visibility = Visibility.Collapsed;
            }
            else
            {
                boxWarningHint.BorderBrush = Brushes.Gray;
                txtWarningHint.Foreground = Brushes.Gray;
                txtWarningHint.Text = "IDLE";
            }
        }

        private void UpdateWarningHint(List<PewsStation> stations)
        {
            int warningCnt = stations.Count((s) => s.Mmi >= 5);
            if (warningCnt >= 2)
            {
                boxWarningHint.BorderBrush = m_redBrush;
                txtWarningHint.Foreground = m_redBrush;
                txtWarningHint.Text = "WARNING";
                ShowWarningHint();
                return;
            }
            
            int cautionCnt = stations.Count((s) => s.Mmi >= 3 && s.Mmi <= 4);
            if (cautionCnt >= 2)
            {
                boxWarningHint.BorderBrush = Brushes.Yellow;
                txtWarningHint.Foreground = Brushes.Yellow;
                txtWarningHint.Text = "CAUTION";
                ShowWarningHint();
                return;
            }
            
            HideWarningHint();
        }

        //#############################################################################################

        private string HandleEqk(int phase, string body, byte[] infoBytes)
        {
            string data = body.Substring(body.Length - (MaxEqkStrLen * 8 + MaxEqkInfoLen));
            string eqkStr = WebUtility.UrlDecode(Encoding.UTF8.GetString(infoBytes));

            double origLat = 30 + (double)Convert.ToInt32(data.Substring(0, 10), 2) / 100;
            double origLon = 124 + (double)Convert.ToInt32(data.Substring(10, 10), 2) / 100;
            double eqkMag = (double)Convert.ToInt32(data.Substring(20, 7), 2) / 10;
            double eqkDep = (double)Convert.ToInt32(data.Substring(27, 10), 2) / 10;
            long eqkUnixTime = Convert.ToInt64(data.Substring(37, 32), 2) + 9 * 3600; // NOTE: UTC어야할텐데 9시간 더해줘야 UTC 시간이 됨.
            var eqkTime = DateTimeOffset.FromUnixTimeSeconds(eqkUnixTime); // UTC
            string eqkId = "20" + Convert.ToInt32(data.Substring(69, 26), 2); // TODO: 22세기가 되면 "20"이 아니게 되는건가?
            int eqkIntens = Convert.ToInt32(data.Substring(95, 4), 2);
            string eqkMaxAreaStr = data.Substring(99, 17);
            var eqkMaxArea = new List<string>();
            if (eqkMaxAreaStr != new string('1', eqkMaxAreaStr.Length))
            {
                for (int i = 0; i < eqkMaxAreaStr.Length; ++i)
                {
                    if (eqkMaxAreaStr[i] == '1')
                    {
                        eqkMaxArea.Add(AreaNames[i]);
                    }
                }
            }

            m_epicenter = new Gdi.PointF((float)LonToX(origLon) - 4, (float)LatToY(origLat) - 7);
            m_waveTick = (float)(((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - m_tide) / 1000.0 - eqkUnixTime) * 3000.0 / 772.5);

            if (phase < 3)
            {
                UpdateEqkInfo(phase, eqkTime.ToLocalTime(), eqkStr, eqkIntens, eqkMag);
            }
            else
            {
                UpdateEqkInfo(phase, eqkTime.ToLocalTime(), eqkStr, eqkIntens, eqkMag, (eqkDep == 0) ? double.NaN : eqkDep);
            }

            string alarmId = eqkId + phase;

            // 페이즈가 넘어갔으며 이전에 전송한 것과 동일한 알람이 아니라면.
            if (phase > m_prevPhase && alarmId != m_prevAlarmId)
            {
                if (phase == 2)
                {
                    // 발생 시각, 규모, 최대 진도, 문구 정도는 부정확할 수 있어도 첫 정보에 포함되는 듯.

                    if (eqkIntens >= 0 && eqkIntens < m_wavHigh.Length)
                    {
                        m_wavHigh[eqkIntens].Stop();
                        m_wavHigh[eqkIntens].Play();
                    }
                    else
                    {
                        m_wavHigh[0].Stop();
                        m_wavHigh[0].Play();
                    }

                    m_prevAlarmId = alarmId;
                }
                else if (phase == 3)
                {
                    // 분석 완료된 것 같고 깊이, 영향 지역이 나옴.

                    if (eqkIntens >= 0 && eqkIntens < m_wavNormal.Length)
                    {
                        m_wavNormal[eqkIntens].Stop();
                        m_wavNormal[eqkIntens].Play();
                    }
                    else
                    {
                        m_wavNormal[0].Stop();
                        m_wavNormal[0].Play();
                    }

                    m_prevAlarmId = alarmId;
                }
            }

            return eqkId;
        }

        private void HandleStn(string body)
        {
            var stnLat = new List<double>();
            var stnLon = new List<double>();

            for (int i = 0; i + 20 <= body.Length; i += 20)
            {
                stnLat.Add(30 + (double)Convert.ToInt32(body.Substring(i, 10), 2) / 100);
                stnLon.Add(120 + (double)Convert.ToInt32(body.Substring(i + 10, 10), 2) / 100);
            }

            if (stnLat.Count < 99)
            {
                // 재시도.
                return;
            }

            m_stnClusters.Clear();
            m_stations.Clear();
            for (int i = 0; i < stnLat.Count; ++i)
            {
                m_stations.Add(new PewsStation
                {
                    Latitude = stnLat[i],
                    Longitude = stnLon[i],
                });
            }

            // 다음 업데이트 신호 대기.
            m_stationUpdate = false;
        }

        private int HandleMmi(string body, int phase)
        {
            const double ClusterDistance = 50.0;

            int maxMmi = 0;

            if (m_stations.Count <= 0)
            {
                return maxMmi;
            }

            var mmiData = new List<int>();

            string mmiBody = body.Split(new[] { "11111111" }, StringSplitOptions.None).First();
            for (int i = 8; i < mmiBody.Length; i += 4)
            {
                if (mmiData.Count >= m_stations.Count)
                {
                    break;
                }

                int mmi = Convert.ToInt32(mmiBody.Substring(i, 4), 2);
                mmiData.Add(mmi);
            }

            if (mmiData.Count < m_stations.Count)
            {
                return maxMmi;
            }

            // 관측소 진도 갱신.
            for (int i = 0; i < m_stations.Count; ++i)
            {
                var stn = m_stations[i];
                int mmi = mmiData[i];

                stn.Mmi = mmi;

                if (mmi > maxMmi)
                {
                    maxMmi = mmi;
                }
            }

            // 클러스터 분석.
            m_stnClusters.Clear();
            if (phase <= 1)
            {
                bool[] visited = new bool[m_stations.Count];
                for (int i = 0; i < m_stations.Count; ++i)
                {
                    if (visited[i])
                    {
                        continue;
                    }

                    var clusterStn = new List<int>();

                    var leftStns = new Queue<int>();
                    leftStns.Enqueue(i);

                    while (leftStns.Count > 0)
                    {
                        int current = leftStns.Dequeue();

                        if (visited[current])
                        {
                            continue;
                        }
                        visited[current] = true;

                        var stn = m_stations[current];
                        int mmi = stn.Mmi;

                        if (mmi < 2)
                        {
                            continue;
                        }

                        clusterStn.Add(current);

                        double centerX = LonToX(stn.Longitude);
                        double centerY = LatToY(stn.Latitude);

                        for (int next = 0; next < m_stations.Count; ++next)
                        {
                            if (visited[next])
                            {
                                continue;
                            }

                            var nextStn = m_stations[next];

                            double subX = LonToX(nextStn.Longitude) - centerX;
                            double subY = LatToY(nextStn.Latitude) - centerY;

                            double distanceSqr = subX * subX + subY + subY;
                            if (distanceSqr < ClusterDistance * ClusterDistance)
                            {
                                leftStns.Enqueue(next);
                            }
                        }
                    }

                    m_stnClusters.Add(clusterStn);
                }
            }

            return maxMmi;
        }

        private async Task RequestGridData(string eqkId, int phase)
        {
            string url = $"{DataPath}/{eqkId}.{(phase == 2 ? 'e' : 'i')}";

            byte[] bytes = null;

            try
            {
                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

                    bytes = await client.DownloadDataTaskAsync(url);
                }
            }
            catch (Exception)
            {
                bytes = null;
            }

            if (bytes == null || bytes.Length <= 0)
            {
                // 나중에 재시도.
                m_updateGrid = true;
                return;
            }

            m_intensityGrid.Clear();
            foreach (byte b in bytes)
            {
                string bStr = ByteToBinStr(b);
                m_intensityGrid.Add(Convert.ToInt32(bStr.Substring(0, 4), 2));
                m_intensityGrid.Add(Convert.ToInt32(bStr.Substring(4, 4), 2));
            }

            m_updateGrid = false;
        }

        //#############################################################################################

        private double LonToX(double longitude)
        {
            return (longitude - 124.5) * 113;
        }

        private double LatToY(double latitude)
        {
            return (38.9 - latitude) * 138.4;
        }

        private Gdi.Color HexCodeToColor(string code)
        {
            if (code.First() != '#')
            {
                throw new ArgumentException();
            }

            code = code.Substring(1);

            if (code.Length == 6)
            {
                code = "FF" + code;
            }

            return Gdi.Color.FromArgb(Convert.ToInt32(code, 16));
        }

        private string ByteToBinStr(byte val)
        {
            return Convert.ToString(val, 2).PadLeft(8, '0');
        }
    }
}
