﻿using Gdi = System.Drawing;
using Gdi2D = System.Drawing.Drawing2D;
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
using System.Windows.Interop;
using System.Windows.Threading;
using System.Net;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.ComponentModel;

using static PewsClient.Util;

namespace PewsClient
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly string OrgDataPath = "https://www.weather.go.kr/pews/data";
        private string DataPath = OrgDataPath;
        private int HeadLength = 4;
        private const int MaxEqkStrLen = 60;
        private const int MaxEqkInfoLen = 120;
        private static string[] AreaNames = { "서울", "부산", "대구", "인천", "광주", "대전", "울산", "세종", "경기", "강원", "충북", "충남", "전북", "전남", "경북", "경남", "제주" };
        private static string[] MmiColors = { "#FFFFFF", "#FFFFFF", "#A0E6FF", "#92D050", "#FFFF00", "#FFC000", "#FF0000", "#A32777", "#632523", "#4C2600", "#000000", "#000000", "#DFDFDF", "#BFBFBF", "#9F9F9F" };
        private static readonly string SimFolder = "sim";

        //#############################################################################################

        public MainWindow()
        {
            InitializeComponent();

            if (DataContext is MainWindowVM vm)
            {
                m_mmiLocations = vm.MmiLocations;
                m_mmiLocations.Clear();

                m_mmiLocationsView = vm.MmiLocationsView;
            }

            UpdateEqkMmiPanel(-1);

            HideEqkInfo();
            HideEqkMmi();
            HideWarningHint();
            HideMmiLocationList();
            HideEta();

            txtTimeSync.Text = $"Sync: {Math.Round(m_tide):F0}ms";
        }

        private ObservableCollection<StationInfoView> m_mmiLocations = null;
        private ICollectionView m_mmiLocationsView = null;

        private readonly string SettingFileName = "settings.txt";
        private UserOption m_option = new UserOption();

        private readonly TimeSpan TimeoutBin = TimeSpan.FromMilliseconds(2000);
        private readonly TimeSpan TimeoutStation = TimeSpan.FromMilliseconds(3000);
        private readonly TimeSpan TimeoutGrid = TimeSpan.FromMilliseconds(5000);

        private bool m_simMode = false;
        private bool m_localSim = false;
        private DateTime m_simEndTime = DateTime.MinValue;

        private Gdi.Font m_mmiFont = new Gdi.Font(Gdi.SystemFonts.DefaultFont.FontFamily, 10.5f, Gdi.FontStyle.Bold);
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
        private MediaPlayer[] m_wavCountdown = new MediaPlayer[60 + 1];

        private Stopwatch m_tickStopwatch = new Stopwatch();
        private DispatcherTimer m_timer = new DispatcherTimer();
        private DispatcherTimer m_timerBeep = new DispatcherTimer();
        private DispatcherTimer m_timerCountdown = new DispatcherTimer();

        private string m_prevBinTime = string.Empty;
        private double m_tide = 1000;
        private DateTime m_nextSyncTime = DateTime.MinValue;
        private DateTime m_serverTime = DateTime.MinValue;

        private int m_prevPhase = 1;
        private int m_phaseDownLeftTick = 0;
        private readonly int PhaseDownDelay = 8;
        private string m_prevAlarmId = string.Empty;
        private DateTimeOffset m_currEqkTime = DateTimeOffset.MinValue;
        private bool m_updateLoc = false;

        private bool m_stationUpdate = true;
        private DateTime m_nextStationUpdate = DateTime.MaxValue;
        private readonly TimeSpan AutoStationUpdateDelay = TimeSpan.FromSeconds(20);
        private List<PewsStation> m_stations = new List<PewsStation>();
        private readonly TimeSpan MaxMmiLifetime = TimeSpan.FromSeconds(60);
        private StationDatabase m_stationDb = new StationDatabase();

        private Gdi.PointF m_epicenter = new Gdi.PointF(-100, -100);
        private float m_waveTick = 0;

        private List<int> m_intensityGrid = new List<int>();
        private bool m_updateGrid = false;
        private List<List<double>> m_intensityWeights = new List<List<double>>();

        private int m_maxMmi = 0;
        private List<List<int>> m_stnClusters = new List<List<int>>();
        private readonly int MinClusterSize = 3;
        private readonly double ClusterDistance = 50.0;

        //#############################################################################################

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();

            m_stationDb.LoadDatabase("res/stations.csv");

            SyncTime();

            StartLocalSimIfExists();

#if DEBUG
            //StartSimulation("2017000407", "20171115142931"); // 포항 5.4
            //StartSimulation("2016000291", "20160912203254"); // 경주 5.8
            //StartSimulation("2019009762", "20190721110418"); // 상주 3.9
            //StartSimulation("2019003859", "20190419111643"); // 동해 4.3
            //StartSimulation("2016000194", "20160705203303"); // 울산 5.0
            //StartSimulation("2019016848", "20191230003208"); // 밀양 3.5
            //StartSimulation("2019001035", "20190210125338"); // 포항 4.1
            //StartSimulation("2018000050", "20180211050303"); // 포항 4.6
            //StartSimulation("2020005363", "20200511194506"); // 북한 3.8
            //StartSimulation("2020018042", "20201218171723"); // 강원 2.7
            //StartSimulation("2021000517", "20210203121756"); // 인천 2.2
            //StartSimulation("2021007178", "20211214171904"); // 제주 4.9

            //StartSimulation("2020123456", "20000101090002", true); // 가상
#endif

            LoadResources();
            DrawCanvas();

            m_timer.Interval = TimeSpan.FromMilliseconds(10);
            m_timer.Tick += Timer_Tick;
            m_timer.Start();

            m_timerBeep.Interval = TimeSpan.FromMilliseconds(2100);
            m_timerBeep.Tick += TimerBeep_Tick;
            m_timerBeep.Start();

            m_timerCountdown.Interval = TimeSpan.FromMilliseconds(100);
            m_timerCountdown.Tick += TimerCountdown_Tick;
            m_timerCountdown.Start();

            Task.Factory.StartNew(() =>
            {
                if (UpdateManager.CheckUpdate())
                {
                    var choice = Dispatcher.Invoke(() => MessageBox.Show("업데이트가 있습니다.\n다운로드 페이지를 여시겠습니까?",
                        "PEWS Client",
                        MessageBoxButton.YesNo, MessageBoxImage.Question));

                    if (choice == MessageBoxResult.Yes)
                    {
                        Process.Start("https://neurowhai.tistory.com/395");
                    }
                }
            });
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            m_timer.Stop();
            m_timerBeep.Stop();
            m_timerCountdown.Stop();

            Properties.Settings.Default.Save();
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

                if (m_localSim)
                {
                    txtTimeSync.Text = $"Sync: Paused";

                    string path = Path.Combine(SimFolder, binTimeStr + ".b");
                    if (File.Exists(path))
                    {
                        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            if (fs.Length > 0)
                            {
                                bytes = new byte[fs.Length];
                                int readBytes = 0;

                                while (readBytes < bytes.Length)
                                {
                                    readBytes += await fs.ReadAsync(bytes, readBytes, bytes.Length - readBytes);
                                }
                            }
                        }
                    }
                }
                else
                {
                    using (var client = new TimeoutWebClient(TimeoutBin))
                    {
                        try
                        {
                            bytes = await client.DownloadDataTaskAsync(url + ".b");
                        }
                        catch (Exception err)
                        {
                            if (err is WebException || err is TimeoutException)
                            {
                                txtStatus.Text = "Loading";

                                if (!m_simMode)
                                {
                                    if (!SyncTime())
                                    {
                                        // 서버 시간과 동기화 실패 시 적절히 오프셋 조정.
                                        if (m_tide < 1000)
                                        {
                                            m_tide += 200;
                                        }
                                        else
                                        {
                                            m_tide -= 200;
                                        }
                                        txtTimeSync.Text = $"Sync: {Math.Round(m_tide):F0}ms";
                                    }
                                }
                            }
                            else
                            {
                                txtStatus.Text = "Error";
                            }

                            return;
                        }


                        if (m_simMode)
                        {
                            txtTimeSync.Text = $"Sync: Paused";
                        }
                        else if (DateTime.UtcNow >= m_nextSyncTime)
                        {
                            // 시간 동기화.
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

                    var bodyBuff = new StringBuilder();
                    for (int i = HeadLength; i < bytes.Length; ++i)
                    {
                        bodyBuff.Append(ByteToBinStr(bytes[i]));
                    }
                    string body = bodyBuff.ToString();


                    if (header[0] == '1')
                    {
                        // 관측소 정보 업데이트 신호 확인.
                        m_stationUpdate = true;
                        if (!m_simMode)
                        {
                            m_nextStationUpdate = binTime + AutoStationUpdateDelay;
                        }
                    }
                    else if (binTime >= m_nextStationUpdate)
                    {
                        // 관측소 정보 업데이트 후 일정 시간 뒤에 한번 더 업데이트.
                        if (!m_simMode)
                        {
                            m_stationUpdate = true;
                        }
                        m_nextStationUpdate = DateTime.MaxValue;
                    }

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

                    int serverPhase = phase;
                    bool phaseRestored = false;

                    // phase가 일시적으로 1로 내려가는 현상이 있어서 껍데기 값 사용.
                    if (phase < m_prevPhase)
                    {
                        if (m_phaseDownLeftTick > 0)
                        {
                            m_phaseDownLeftTick -= 1;
                            if (m_phaseDownLeftTick <= 0)
                            {
                                // phase가 실질적으로 내려갔다고 봄.
                                m_phaseDownLeftTick = 0;
                            }
                            else
                            {
                                // 기존 phase 유지.
                                phase = m_prevPhase;
                            }
                        }
                        else
                        {
                            // 기존 phase 유지 시작.
                            m_phaseDownLeftTick = PhaseDownDelay;
                            phase = m_prevPhase;
                        }
                    }
                    else
                    {
                        // phase가 원복되었음을 확인.
                        if (m_phaseDownLeftTick > 0 && phase > 1 && phase == m_prevPhase)
                        {
                            phaseRestored = true;
                        }

                        m_phaseDownLeftTick = 0;
                    }

                    bool phaseChanged = (phase != m_prevPhase);


                    if (phase > 1)
                    {
                        string eqkId = HandleEqk(serverPhase, body);

                        ShowEqkInfo();

                        if (m_option.HomeAvailable)
                        {
                            ShowEta();
                        }

                        if (phase == 2 && phaseChanged)
                        {
                            // 속보 전환 시 지역별 계측진도 목록 초기화.
                            ClearMmiLocationList();

                            int maxMaxMmi = 0;
                            foreach (var stn in m_stations)
                            {
                                if (stn.MaxMmi > maxMaxMmi)
                                {
                                    maxMaxMmi = stn.MaxMmi;
                                }

                                if (stn.MaxMmi >= 2)
                                {
                                    AddMmiLocationList(stn.MaxMmi, stn.Name, stn.Location);
                                }
                            }

                            ShowMmiLocationList();

                            // 계측진도 이전 기록에서 찾아 표시.
                            if (maxMaxMmi >= m_maxMmi)
                            {
                                m_maxMmi = maxMaxMmi;
                                UpdateEqkMmiPanel(maxMaxMmi);
                            }
                        }

                        if (phaseChanged || phaseRestored || m_updateLoc)
                        {
                            var infoBytes = bytes.Skip(bytes.Length - MaxEqkStrLen).ToArray();
                            txtEqkLoc.Text = WebUtility.UrlDecode(Encoding.UTF8.GetString(infoBytes)).Trim();

                            if (!string.IsNullOrWhiteSpace(eqkId) && !m_localSim)
                            {
                                m_updateLoc = true;
                                string eqkLoc = await RequestLocData(eqkId, phase);
                                if (!string.IsNullOrEmpty(eqkLoc))
                                {
                                    txtEqkLoc.Text = eqkLoc;
                                }
                            }
                        }

                        if ((phaseChanged || phaseRestored || m_updateGrid) && !string.IsNullOrWhiteSpace(eqkId))
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
                        HideMmiLocationList();
                        HideEta();

                        m_currEqkTime = DateTimeOffset.MinValue;

                        if (m_prevPhase > 1)
                        {
                            foreach (var stn in m_stations)
                            {
                                stn.ResetMaxMmi();
                            }

                            ClearMmiLocationList();

                            // 다음 상황 때 재생 가능하도록 초기화.
                            ResetCountdownWav();

                            m_wavEnd.Stop();
                            m_wavEnd.Play();
                        }
                    }

                    m_prevPhase = phase;


                    // 관측소 데이터 갱신이 필요하면.
                    if (m_stationUpdate)
                    {
                        byte[] stnBytes = null;

                        if (m_localSim)
                        {
                            string path = Path.Combine(SimFolder, "stations.s");
                            if (File.Exists(path))
                            {
                                stnBytes = File.ReadAllBytes(path);
                                m_stationUpdate = false;
                            }
                        }
                        else
                        {
                            using (var client = new TimeoutWebClient(TimeoutStation))
                            {
                                for (int retry = 0; retry <= 1; ++retry)
                                {
                                    try
                                    {
                                        stnBytes = await client.DownloadDataTaskAsync(url + ".s");
                                        break;
                                    }
                                    catch
                                    {
                                        stnBytes = null;
                                        await Task.Delay(300);
                                    }
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


                    // 관측소 목록이 있고 위에서 관측소 데이터 갱신시 성공했다면 관측소 진도 분석.
                    if (m_stations.Count > 0 && !m_stationUpdate)
                    {
                        int maxMmi = HandleMmi(body, phase);


                        // 경고 힌트 갱신.
                        UpdateWarningHint(m_stations);


                        // 강도별 관측소 수.
                        txtHighStn.Text = m_stations.Count((stn) => (stn.Mmi >= 5)).ToString();
                        txtMidStn.Text = m_stations.Count((stn) => (stn.Mmi >= 3 && stn.Mmi <= 4)).ToString();
                        txtLowStn.Text = m_stations.Count((stn) => (stn.Mmi == 2)).ToString();


                        // 계측진도를 띄우거나 갱신할지 여부 결정.
                        bool eqkMayOccured = (maxMmi > 3 || phase > 1);
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

                                // 클러스터의 최대진도가 1보다 크거나
                                // 크기가 기준의 n배 이상일 경우 유효함.
                                if (clusterMmi > 1 || cluster.Count >= MinClusterSize * 4)
                                {
                                    if (clusterMmi >= maxMmi)
                                    {
                                        // 전역 최대진도와 같다는 것을 확인하였으니 바로 탈출.
                                        maxClusterMmi = clusterMmi;
                                        break;
                                    }
                                    else if (clusterMmi > maxClusterMmi)
                                    {
                                        maxClusterMmi = clusterMmi;
                                    }
                                }
                            }

                            maxMmi = maxClusterMmi;
                            if (maxClusterMmi >= 1)
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
                        m_maxMmi = 0;
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
                long elapsed = m_tickStopwatch.ElapsedMilliseconds;

                if (bCheckLag)
                {
                    txtTickDelay.Text = $"Lag: {elapsed}ms";
                }

                var leftDelay = 100L - elapsed;
                if (leftDelay > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(leftDelay));
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

        private void TimerCountdown_Tick(object sender, EventArgs e)
        {
            if (m_currEqkTime != DateTimeOffset.MinValue
                && m_option.HomeAvailable
                && m_prevPhase > 1)
            {
                double leftTime = Math.Floor(Math.Sqrt(Math.Pow((m_epicenter.Y - m_option.HomeLatitude) * 111, 2) + Math.Pow((m_epicenter.X - m_option.HomeLongitude) * 88, 2)) / 3);
                leftTime -= Math.Ceiling(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (m_tide / 1000.0) - m_currEqkTime.ToUnixTimeSeconds());

                UpdateEta(leftTime);

                // 카운트다운 재생.
                int time = (int)(Math.Ceiling(leftTime) + double.Epsilon);
                if (time >= 0 && time < m_wavCountdown.Length
                    && m_wavCountdown[time] != null)
                {
                    // 중복으로 호출될 수 있지만 기존 Play 중엔 영향이 없는 것을 이용.
                    // 상황 종료 시 Stop 호출 필요.
                    m_wavCountdown[time].Play();
                }
            }
            else
            {
                lblHomeMmi.Text = "예상 진도";
                lblEta.Text = "도달 시간 표시";
            }
        }

        private void ImageCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 위치 설정 모드가 아니면 무시.
            if (chkSetLocation.IsChecked != true)
            {
                return;
            }

            var cursor = e.GetPosition(canvas);
            double x = cursor.X / canvas.RenderSize.Width * m_imgMap.Width;
            double y = cursor.Y / canvas.RenderSize.Height * m_imgMap.Height;

            m_option.HomeLongitude = XToLon(x);
            m_option.HomeLatitude = YToLat(y);

            chkSetLocation.IsChecked = false;

            if (chkPin.IsChecked)
            {
                ShowEta();
            }

            if (m_prevPhase > 1)
            {
                UpdateHomeMmi(m_intensityGrid);
                ResetCountdownWav();
            }

            SaveSettings();
        }

        //#############################################################################################

        private void CheckBoxPin_Changed(object sender, RoutedEventArgs e)
        {
            var box = sender as MenuItem;
            if (box.IsChecked == true)
            {
                ShowEqkInfo();
                ShowEqkMmi();
                ShowWarningHint();
                ShowMmiLocationList();

                if (m_option.HomeAvailable)
                {
                    ShowEta();
                }
            }
        }

        private void MenuItemRemoveHome_Click(object sender, RoutedEventArgs e)
        {
            m_option.RemoveHome();

            HideEta(ignorePin: true);

            SaveSettings();
        }

        private void CheckBoxGridNone_Click(object sender, RoutedEventArgs e)
        {
            CheckBoxGrid_Click(sender as MenuItem, RtGridModes.None);

            if (m_prevPhase <= 1)
            {
                m_intensityGrid.Clear();
            }
        }

        private void CheckBoxGridCurrent_Click(object sender, RoutedEventArgs e)
        {
            CheckBoxGrid_Click(sender as MenuItem, RtGridModes.Current);
        }

        private void CheckBoxGridMax_Click(object sender, RoutedEventArgs e)
        {
            CheckBoxGrid_Click(sender as MenuItem, RtGridModes.Max);
        }

        private void CheckBoxGrid_Click(MenuItem menu, RtGridModes mode)
        {
            if (menu.IsChecked)
            {
                if (chkGridNone != menu)
                {
                    chkGridNone.IsChecked = false;
                }
                if (chkGridCurrent != menu)
                {
                    chkGridCurrent.IsChecked = false;
                }
                if (chkGridMax != menu)
                {
                    chkGridMax.IsChecked = false;
                }

                m_option.RtGridMode = mode;
                SaveSettings();
            }
            else
            {
                menu.IsChecked = true;
            }
        }

        //#############################################################################################

        private bool SyncTime()
        {
            try
            {
                var serverTime = TimeManager.GetNetworkTime("time.windows.com", 3000);
                m_tide = (DateTime.UtcNow - serverTime).TotalMilliseconds + 1000;
                txtTimeSync.Text = $"Sync: {Math.Round(m_tide):F0}ms";
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

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
                new Gdi.Pen(Gdi.Color.LightSeaGreen, 3.5f),
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

            for (int time = 0; time < m_wavCountdown.Length; ++time)
            {
                string wavPath = $"res/time{time}.mp3";
                if (File.Exists(wavPath))
                {
                    var player = new MediaPlayer();
                    player.Open(new Uri(wavPath, UriKind.Relative));
                    m_wavCountdown[time] = player;
                }
                else
                {
                    m_wavCountdown[time] = null;
                }
            }

            m_wavBeep.Volume = 0.25;
            m_wavBeep1.Volume = 0.25;
            m_wavBeep2.Volume = 0.25;
            m_wavBeep3.Volume = 0.25;
        }

        private void LoadSettings()
        {
            try
            {
                m_option.Load(SettingFileName);
            }
            catch (Exception e)
            {
                MessageBox.Show("설정을 완전하게 불러올 수 없습니다.\nError: " + e.Message,
                    "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            chkGridNone.IsChecked = (m_option.RtGridMode == RtGridModes.None);
            chkGridCurrent.IsChecked = (m_option.RtGridMode == RtGridModes.Current);
            chkGridMax.IsChecked = (m_option.RtGridMode == RtGridModes.Max);
        }

        private void SaveSettings()
        {
            try
            {
                m_option.Save(SettingFileName);
            }
            catch (Exception e)
            {
                MessageBox.Show("설정을 저장할 수 없습니다.\nError: " + e.Message,
                    "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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

            float eqkX = (float)LonToX(m_epicenter.X) - 4;
            float eqkY = (float)LatToY(m_epicenter.Y) - 7;

            using (var g = Gdi.Graphics.FromImage(m_canvasBitmap))
            {
                // Background
                g.Clear(Gdi.Color.FromArgb(211, 211, 211));

                // Intensity
                var mmiIterator = m_intensityGrid.GetEnumerator();
                int minMmi = 0;
                if (m_prevPhase <= 1)
                {
                    minMmi = 2;
                }
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

                        if (mmi >= minMmi && mmi < m_mmiBrushes.Length)
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
                var stations = (m_prevPhase == 2 ? m_stations.OrderBy((s) => s.MaxMmi).AsEnumerable() : m_stations);
                foreach (var stn in stations)
                {
                    float x = (float)(stn.X - 4);
                    float y = (float)(stn.Y - 4);

                    if (m_prevPhase == 2 && stn.MaxMmi >= 2)
                    {
                        // 지진 속보 시.
                        int maxMmi = stn.MaxMmi;
                        if (maxMmi >= 0 && maxMmi < m_mmiBrushes.Length)
                        {
                            var brush = m_mmiBrushes[maxMmi];
                            var backBrush = (stn.MaxMmi >= 6 ? Gdi.Brushes.White : Gdi.Brushes.Black);
                            var stringFormat = new Gdi.StringFormat
                            {
                                Alignment = Gdi.StringAlignment.Center,
                                LineAlignment = Gdi.StringAlignment.Center,
                            };

                            x += 4;
                            y += 4;
                            g.FillEllipse(brush, x - 11, y - 11, 22, 22);
                            g.DrawString(Earthquake.MMIToString(stn.MaxMmi), m_mmiFont,
                                backBrush, x + 0.5f, y + 0.5f, stringFormat);
                        }
                    }
                    else
                    {
                        // 평시.
                        int mmi = stn.RawMmi;
                        if (mmi >= 0 && mmi < m_mmiBrushes.Length)
                        {
                            var brush = m_mmiBrushes[mmi];
                            g.FillRectangle(brush, x, y, 10, 10);
                        }
                        g.DrawRectangle(Gdi.Pens.Black, x, y, 10, 10);
                    }
                }

                // Home
                if (m_option.HomeAvailable)
                {
                    float x = (float)(LonToX(m_option.HomeLongitude));
                    float y = (float)(LatToY(m_option.HomeLatitude));
                    g.FillPie(Gdi.Brushes.DeepPink, x - 14, y - 26, 28, 52, -90f - 20f, 40f);
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
                        float x = (float)stn.X;
                        float y = (float)stn.Y;

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

                    // 클러스터의 최대진도가 1 이상이고
                    // 1 이하일 땐 클러스터 크기가 기준의 n배 이상일 때만 표시.
                    if (maxMmi >= 1 && (maxMmi > 1 || cluster.Count >= MinClusterSize * 2))
                    {
                        int mmiStage = 0;
                        if (maxMmi >= 5)
                        {
                            mmiStage = 3;
                        }
                        else if (maxMmi >= 3)
                        {
                            mmiStage = 2;
                        }
                        else if (maxMmi >= 2)
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
                    using (var sWavePen = new Gdi.Pen(Gdi.Color.FromArgb(255, 0, 0), 2.0f))
                    using (var pWavePen = new Gdi.Pen(Gdi.Color.FromArgb(0, 0, 255), 2.0f))
                    using (var radialPath = new Gdi2D.GraphicsPath())
                    {
                        // P
                        g.DrawEllipse(pWavePen, eqkX - m_waveTick * 2, eqkY - m_waveTick * 2,
                            m_waveTick * 4, m_waveTick * 4);

                        // S
                        radialPath.AddEllipse(eqkX - m_waveTick, eqkY - m_waveTick,
                            m_waveTick * 2, m_waveTick * 2);
                        using (var gradBrush = new Gdi2D.PathGradientBrush(radialPath))
                        {
                            const float WaveThickness = 10.0f;
                            if (m_waveTick > WaveThickness)
                            {
                                float focusScale = (m_waveTick - WaveThickness) / m_waveTick;
                                gradBrush.FocusScales = new Gdi.PointF(focusScale, focusScale);
                            }
                            gradBrush.CenterColor = Gdi.Color.FromArgb(0, Gdi.Color.Red);
                            gradBrush.SurroundColors = new[] { Gdi.Color.Red };
                            g.FillEllipse(gradBrush, eqkX - m_waveTick, eqkY - m_waveTick,
                                m_waveTick * 2, m_waveTick * 2);
                        }
                    }
                }

                // Epicenter
                if (m_prevPhase > 1
                    && eqkX > -32 && eqkX < fWidth + 32
                    && eqkY > -32 && eqkY < fHeight + 32)
                {
                    g.FillEllipse(Gdi.Brushes.Blue, eqkX - 4, eqkY - 4, 8, 8);
                    g.DrawEllipse(Gdi.Pens.Blue, eqkX - 8, eqkY - 8, 16, 16);
                    g.DrawEllipse(Gdi.Pens.Blue, eqkX - 12, eqkY - 12, 24, 24);
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

            canvas.InvalidateVisual();
        }

        private void StartSimulation(string eqkId, string eqkStartTime, bool local = false, int headLen = 1)
        {
            var startTime = DateTime.ParseExact(eqkStartTime, "yyyyMMddHHmmss", CultureInfo.InvariantCulture);

            m_simMode = true;
            m_localSim = local;
            m_simEndTime = startTime.AddHours(-9) + TimeSpan.FromSeconds(300);

            HeadLength = headLen;
            DataPath = OrgDataPath + $"/{eqkId}";
            m_tide = (DateTime.UtcNow.AddHours(9) - startTime).TotalMilliseconds;
        }

        private void StopSimulation()
        {
            m_simMode = false;
            m_localSim = false;

            HeadLength = 4;
            DataPath = OrgDataPath;
            m_tide = 1000;

            m_phaseDownLeftTick = 1; // Phase 유지 안 하고 바로 낮추도록 함.

            m_stationUpdate = true;
            m_nextStationUpdate = DateTime.MaxValue;
        }

        private void StartLocalSimIfExists()
        {
            try
            {
                if (!File.Exists(Path.Combine(SimFolder, "stations.s")))
                {
                    return;
                }

                string bFile = Directory.EnumerateFiles(SimFolder, "*.b").Skip(2).FirstOrDefault();
                string eFile = Directory.EnumerateFiles(SimFolder, "*.e").FirstOrDefault();

                if (string.IsNullOrEmpty(bFile) || string.IsNullOrEmpty(eFile))
                {
                    return;
                }

                string id = Path.GetFileNameWithoutExtension(eFile);

                string startTimeStr = Path.GetFileNameWithoutExtension(bFile);
                var startTime = DateTime.ParseExact(startTimeStr, "yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                startTime += TimeSpan.FromHours(9);
                startTimeStr = startTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

                int headLen = 1;
                string headFile = Path.Combine(SimFolder, "head.txt");
                if (File.Exists(headFile))
                {
                    int.TryParse(File.ReadLines(headFile).FirstOrDefault() ?? "1", out headLen);
                    if (headLen > 4)
                    {
                        headLen = 4;
                    }
                    else if (headLen < 1)
                    {
                        headLen = 1;
                    }
                }

                StartSimulation(id, startTimeStr, true, headLen);
            }
            catch
            {
                StopSimulation();
            }
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

        private void UpdateEqkInfo(int phase, DateTimeOffset time, int mmi, double magnitude, double depth = double.NaN)
        {
            if (phase <= 1)
            {
                return;
            }

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

            m_maxMmi = 0; // 다음에 계측진도가 처음부터 갱신될 수 있도록 초기화.
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

            if (level < Earthquake.MMIToMinimumGal(2))
            {
                // 진도 2가 하나라도 있으면 알림을 재생하도록 여긴 scale을 곱한 기준을 쓰지 않음.
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

        private void ShowMmiLocationList()
        {
            boxStationLoc.Visibility = Visibility.Visible;
        }

        private void HideMmiLocationList()
        {
            if (chkPin.IsChecked != true)
            {
                boxStationLoc.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateMmiLocationList(int mmi, string stnName, string location)
        {
            if (string.IsNullOrEmpty(stnName))
            {
                return;
            }

            if (mmi < 0)
            {
                mmi = 0;
            }
            else if (mmi >= m_mmiWpfBrushes.Length)
            {
                mmi = m_mmiWpfBrushes.Length - 1;
            }

            foreach (var loc in m_mmiLocations)
            {
                if (loc.Name == stnName)
                {
                    // 진도가 상승했을 때만 갱신.
                    if (mmi > loc.Mmi)
                    {
                        loc.Mmi = mmi;
                        loc.MmiBrush = (mmi >= 6 ? Brushes.White : Brushes.Black);
                        loc.MmiBackBrush = m_mmiWpfBrushes[mmi];

                        m_mmiLocationsView.Refresh();
                    }
                    return;
                }
            }

            AddMmiLocationList(mmi, stnName, location);
        }

        private void AddMmiLocationList(int mmi, string stnName, string location)
        {
            m_mmiLocations.Add(new StationInfoView
            {
                Mmi = mmi,
                MmiBrush = (mmi >= 6 ? Brushes.White : Brushes.Black),
                MmiBackBrush = m_mmiWpfBrushes[mmi],
                Name = stnName,
                Location = location,
            });

            m_mmiLocationsView.Refresh();
        }

        private void ClearMmiLocationList()
        {
            m_mmiLocations.Clear();
            m_mmiLocationsView.Refresh();
        }

        private void ShowEta()
        {
            lblHomeMmi.Visibility = Visibility.Visible;
            lblEta.Visibility = Visibility.Visible;
        }

        private void HideEta(bool ignorePin = false)
        {
            if (ignorePin || chkPin.IsChecked != true)
            {
                lblHomeMmi.Visibility = Visibility.Collapsed;
                lblEta.Visibility = Visibility.Collapsed;
            }

            lblHomeMmi.Text = "예상 진도";
            lblEta.Text = "도달 시간 표시";
        }

        private void UpdateEta(double eta)
        {
            if (Math.Ceiling(eta) > 0)
            {
                if (eta >= 60)
                {
                    lblEta.Text = $"도달 {eta / 60:F0}분 전";
                }
                else
                {
                    lblEta.Text = $"도달 {eta:F0}초 전";
                }
            }
            else
            {
                lblEta.Text = "도달";
            }
        }

        private void UpdateHomeMmi(List<int> intensityList)
        {
            if (intensityList.Count <= 0)
            {
                return;
            }

            const int X_SIZE = (int)((132.05 - 124.5) / 0.05);

            if (m_option.HomeAvailable)
            {
                int mmi = -1;

                // 내 위치로 인덱스 계산.
                int yIdx = (int)Math.Round((38.85 - m_option.HomeLatitude) / 0.05);
                int xIdx = (int)Math.Round((m_option.HomeLongitude - 124.5) / 0.05);
                int index = yIdx * X_SIZE + xIdx;

                if (yIdx >= 0 && xIdx >= 0 && xIdx < X_SIZE
                    && index >= 0 && index < intensityList.Count)
                {
                    mmi = intensityList[index];
                }

                if (mmi < 0)
                {
                    lblHomeMmi.Text = "진도 불명";
                }
                else
                {
                    lblHomeMmi.Text = $"진도 {Earthquake.MMIToString(mmi)}";
                }
            }
        }

        private void ResetCountdownWav()
        {
            for (int time = 0; time < m_wavCountdown.Length; ++time)
            {
                var wav = m_wavCountdown[time];
                if (wav != null)
                {
                    wav.Stop();
                }
            }
        }

        //#############################################################################################

        private string HandleEqk(int phase, string body)
        {
            if (phase <= 1)
            {
                return m_prevAlarmId.Split('|').First();
            }

            string data = body.Substring(body.Length - (MaxEqkStrLen * 8 + MaxEqkInfoLen));

            double origLat = 30 + (double)Convert.ToInt32(data.Substring(0, 10), 2) / 100;
            double origLon = 124 + (double)Convert.ToInt32(data.Substring(10, 10), 2) / 100;
            double eqkMag = (double)Convert.ToInt32(data.Substring(20, 7), 2) / 10;
            double eqkDep = (double)Convert.ToInt32(data.Substring(27, 10), 2) / 10;
            long eqkUnixTime = Convert.ToInt64(data.Substring(37, 32), 2) + 9 * 3600; // 초 // NOTE: UTC어야할텐데 9시간 더해줘야 UTC 시간이 됨.
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

            m_epicenter = new Gdi.PointF((float)origLon, (float)origLat);
            m_waveTick = (float)(((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - m_tide) / 1000.0 - eqkUnixTime) * 3000.0 / 772.5);
            m_currEqkTime = eqkTime;

            if (phase == 2)
            {
                UpdateEqkInfo(phase, eqkTime.ToLocalTime(), eqkIntens, eqkMag);
            }
            else if (phase == 3)
            {
                UpdateEqkInfo(phase, eqkTime.ToLocalTime(), eqkIntens, eqkMag, (eqkDep == 0) ? double.NaN : eqkDep);
            }

            string alarmId = $"{eqkId}|{phase}";

            // 페이즈가 넘어갔으며 이전에 전송한 것과 동일한 알람이 아니라면.
            if (phase != m_prevPhase && alarmId != m_prevAlarmId)
            {
                if (phase == 2)
                {
                    // 발생 시각, 규모, 최대진도, 문구 정도는 부정확할 수 있어도 첫 정보에 포함되는 듯.

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
                double lat = (double)Convert.ToInt32(body.Substring(i, 10), 2) / 100;
                double lon = (double)Convert.ToInt32(body.Substring(i + 10, 10), 2) / 100;
                stnLat.Add(30 + lat);

                // KS_ULDR(37.48, 130.89), KS_TAHA(37.51, 130.81)
                if (lat.AlmostEqualTo(7.48) && lon.AlmostEqualTo(0.89)
                    || lat.AlmostEqualTo(7.51) && lon.AlmostEqualTo(0.81))
                {
                    stnLon.Add(130 + lon);
                }
                else
                {
                    stnLon.Add(120 + lon);
                }
            }

            if (stnLat.Count < 99)
            {
                // 재시도.
                return;
            }

            ClearMmiLocationList();
            m_stnClusters.Clear();
            m_stations.Clear();

            var stnNameSet = new HashSet<string>();
            for (int i = 0; i < stnLat.Count; ++i)
            {
                var info = m_stationDb.GetStationInfoAround(stnLat[i], stnLon[i]);

#if DEBUG
                // NOTE: 과거와 현재 같은 관측소라도 위치가 미세하게 다른 경우 있으니 최근 것으로만 확인해야 함.
                if (string.IsNullOrEmpty(info.Name))
                {
                    Debug.WriteLine($"Can not found a station on {stnLat[i]}, {stnLon[i]}.");
                }
#endif

                // 이름 충돌 대응.
                string name = string.IsNullOrEmpty(info.Name) ? $"IDX{i}" : info.Name;
                if (stnNameSet.Contains(name))
                {
                    name = $"{name}-{i}";
                }
                stnNameSet.Add(name);

                m_stations.Add(new PewsStation
                {
                    Name = name,
                    Location = string.IsNullOrEmpty(info.Location) ? $"{i + 1}번" : info.Location,
                    Latitude = stnLat[i],
                    Longitude = stnLon[i],
                });

#if DEBUG
                for (int j = 0; j < i; ++j)
                {
                    if (m_stations[j].Name == m_stations[i].Name)
                    {
                        Debug.WriteLine($"{m_stations[i].Name} 중복!");
                    }
                }
#endif
            }

            // 인근 관측소 목록 생성.
            for (int i = 0; i < m_stations.Count; ++i)
            {
                var center = m_stations[i];

                double centerX = center.X;
                double centerY = center.Y;

                for (int j = i + 1; j < m_stations.Count; ++j)
                {
                    var other = m_stations[j];

                    double subX = other.X - centerX;
                    double subY = other.Y - centerY;

                    double distanceSqr = subX * subX + subY * subY;
                    if (distanceSqr < ClusterDistance * ClusterDistance)
                    {
                        center.Nodes.Add(j);
                        other.Nodes.Add(i);
                    }
                }
            }

            // IDW 보간을 위한 그리드 셀-관측소간 가중치 계산.
            m_intensityWeights.Clear();
            for (double i = 38.85; i > 33; i -= 0.05)
            {
                for (double j = 124.5; j < 132.05; j += 0.05)
                {
                    float x = (float)LonToX(j);
                    float y = (float)(LatToY(i) - 3);

                    var weights = new List<double>();
                    weights.Add(0.0);

                    m_intensityWeights.Add(weights);

                    foreach (var stn in m_stations)
                    {
                        double subX = x - stn.X;
                        double subY = y - stn.Y;
                        double distanceSq = subX * subX + subY * subY;

                        if (distanceSq < 1.0)
                        {
                            weights.Add(1.0);
                        }
                        else
                        {
                            weights.Add(1.0 / (distanceSq * distanceSq * distanceSq));
                        }
                        weights[0] += weights[weights.Count - 1];
                    }
                }
            }

            // 다음 업데이트 신호 대기.
            m_stationUpdate = false;
        }

        private int HandleMmi(string body, int phase)
        {
            int maxMmi = 0;

            if (m_stations.Count <= 0)
            {
                return maxMmi;
            }
            
            var mmiData = new List<int>();

            for (int i = 0; i < body.Length; i += 4)
            {
                if (mmiData.Count >= m_stations.Count)
                {
                    break;
                }

                int mmi = Convert.ToInt32(body.Substring(i, 4), 2);
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
                int rawMmi = mmiData[i];

                int mmi = rawMmi;
                if (mmi < 0)
                {
                    // 음수일 수 없음.
                    mmi = 0;
                    rawMmi = 0;
                }
                else if (mmi > 11)
                {
                    // 세분화 된 진도 I.
                    mmi = 1;
                }
                else if (mmi > 10)
                {
                    // 이도저도 아님.
                    // PEWS 사이트에서는 진도 X에 해당하는 색을 의미하긴 함.
                    mmi = 10;
                }

                int prevMaxMmi = stn.MaxMmi;
                stn.UpdateMmi(mmi, MaxMmiLifetime);
                stn.RawMmi = rawMmi;

                // 속보 상황이고 관측소 최대진도가 직전 대비 올랐고 그것이 2 이상일 때
                // 해당 지역 계측진도 갱신 시도.
                if (phase == 2 && prevMaxMmi < stn.MaxMmi && stn.MaxMmi >= 2)
                {
                    UpdateMmiLocationList(stn.MaxMmi, stn.Name, stn.Location);
                }

                if (mmi > maxMmi)
                {
                    maxMmi = mmi;
                }
            }

            // 클러스터 분석.
            m_stnClusters.Clear();
            if (phase != 2)
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
                    visited[i] = true;

                    while (leftStns.Count > 0)
                    {
                        int current = leftStns.Dequeue();

                        var stn = m_stations[current];
                        int mmi = stn.Mmi;

                        // 진도 2 이상이거나 진도 1 안에서 큰 축에 속하는 경우에만 노드로 취급.
                        if (mmi < 2 && stn.RawMmi < 14)
                        {
                            continue;
                        }

                        clusterStn.Add(current);

                        foreach (int next in stn.Nodes)
                        {
                            if (visited[next])
                            {
                                continue;
                            }

                            leftStns.Enqueue(next);
                            visited[next] = true;
                        }
                    }

                    m_stnClusters.Add(clusterStn);
                }
            }

            // 실시간 진도 그리드 계산.
            if (phase <= 1 && m_option.RtGridMode != RtGridModes.None)
            {
                if (m_option.RtGridMode == RtGridModes.Current)
                {
                    UpdateRealtimeGrid(PewsStation.IndexMmi);
                }
                else if (m_option.RtGridMode == RtGridModes.Max)
                {
                    UpdateRealtimeGrid(PewsStation.IndexMaxMmi);
                }
            }

            return maxMmi;
        }

        private void UpdateRealtimeGrid(int mmiIndex)
        {
            m_intensityGrid.Clear();

            int w = 0;
            for (double i = 38.85; i > 33; i -= 0.05)
            {
                for (double j = 124.5; j < 132.05; j += 0.05)
                {
                    if (w >= m_intensityWeights.Count)
                    {
                        break;
                    }
                    var weights = m_intensityWeights[w];
                    w += 1;

                    int minCnt = Math.Min(weights.Count - 1, m_stations.Count);
                    double sum = 0;
                    for (int z = 0; z < minCnt; z++)
                    {
                        sum += weights[1 + z] * m_stations[z].MmiData[mmiIndex];
                    }
                    int mmi = (int)Math.Round(sum / weights[0]);

                    m_intensityGrid.Add(mmi);
                }
            }
        }

        private async Task RequestGridData(string eqkId, int phase)
        {
            byte[] bytes = null;

            try
            {
                if (m_localSim)
                {
                    string path = Path.Combine(SimFolder, $"{eqkId}.{(phase == 2 ? 'e' : 'i')}");
                    if (File.Exists(path))
                    {
                        bytes = File.ReadAllBytes(path);
                    }
                }
                else
                {
                    using (var client = new TimeoutWebClient(TimeoutGrid))
                    {
                        string url = $"{DataPath}/{eqkId}.{(phase == 2 ? 'e' : 'i')}";
                        bytes = await client.DownloadDataTaskAsync(url);
                    }
                }
            }
            catch
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
                for (int offset = 0; offset < 8; offset += 4)
                {
                    int mmi = Convert.ToInt32(bStr.Substring(offset, 4), 2);
                    if (mmi < 0)
                    {
                        mmi = 0;
                    }
                    else if (mmi > 11)
                    {
                        // 세분화 된 진도 I.
                        mmi = 1;
                    }

                    m_intensityGrid.Add(mmi);
                }
            }

            // 예상 도달 진도 계산.
            UpdateHomeMmi(m_intensityGrid);

            m_updateGrid = false;
        }

        private async Task<string> RequestLocData(string eqkId, int phase)
        {
            byte[] bytes = null;

            try
            {
                string fileName = string.Empty;
                if (phase == 2)
                {
                    fileName = $"{eqkId}.le";
                }
                else if (phase == 3)
                {
                    fileName = $"{eqkId}.li";
                }
                else
                {
                    m_updateLoc = false;
                    return string.Empty;
                }

                if (!string.IsNullOrEmpty(fileName))
                {
                    using (var client = new TimeoutWebClient(TimeoutGrid))
                    {
                        string url = $"{DataPath}/{fileName}";
                        bytes = await client.DownloadDataTaskAsync(url);
                    }
                }
            }
            catch
            {
                bytes = null;
            }

            if (bytes == null || bytes.Length <= 0)
            {
                // 나중에 재시도.
                m_updateLoc = true;
                return string.Empty;
            }

            m_updateLoc = false;

            string json = Encoding.UTF8.GetString(bytes);

            string propName = "\"info_ko\"";
            int idx = json.IndexOf(propName);
            if (idx < 0)
            {
                return string.Empty;
            }

            idx = json.IndexOf('"', idx + propName.Length);
            if (idx < 0)
            {
                return string.Empty;
            }

            int endIdx = json.IndexOf('"', idx + 1);
            if (endIdx < 0)
            {
                return string.Empty;
            }

            return json.Substring(idx + 1, endIdx - idx - 1).Trim();
        }

        //#############################################################################################

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
