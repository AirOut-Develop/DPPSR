using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using AOIDSClib;
using AOIDSClib.Licensing;
using AOIDSClib.Models;
using Microsoft.Win32;
using System.Net.Http;

namespace DPPSR
{
    public partial class MainWindow : Window
    {
        // REST 라이선스 검증 요청에 사용할 공유 HttpClient
        private static readonly HttpClient LicenseHttpClient = new();

        // 테스트용 기본 라이선스 키 (실제 서비스에서는 사용자 입력 필요)
        private const string DefaultLicenseKey = "LICENS_EKEY";

        // AOIDSClib에 넘길 분석 옵션. tessdata 경로 등 기본값만 지정하고 라이선스는 UI에서 구성.
        private readonly CardAnalyzerOptions _options = new()
        {
            TessDataPath = Path.Combine(AppContext.BaseDirectory, "tessdata"),
            EnableAutoRotate180 = false
        };

        // 복수 번 분석 시 재사용할 CardAnalyzer 인스턴스
        private CardAnalyzer? _analyzer;
        // 현재 선택된 이미지 경로. 없으면 분석 버튼 비활성화.
        private string? _selectedImagePath;

        public MainWindow()
        {
            InitializeComponent();
            StatusText.Text = "분석할 이미지를 불러와 주세요.";
            // REST 기반 라이선스 검증기를 등록하고 기본 키를 미리 적용한다.
            _options.LicenseRegistry = new RestLicenseRegistry(LicenseHttpClient);
            LicenseKeyTextBox.Text = DefaultLicenseKey;
            _ = ApplyLicenseKeyAsync(DefaultLicenseKey, updateStatus: false);
        }

        private void OnLoadImageClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "이미지 파일 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|모든 파일 (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            _selectedImagePath = dialog.FileName;
            try
            {
                // 이미지 미리보기를 표시해 사용자가 올바른 파일을 선택했는지 확인할 수 있게 함.
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(_selectedImagePath);
                bitmap.EndInit();
                bitmap.Freeze();
                PreviewImage.Source = bitmap;
                StatusText.Text = $"이미지 로드 완료: {Path.GetFileName(_selectedImagePath)}";
                //AnalyzeButton.IsEnabled = !string.IsNullOrWhiteSpace(_options.LicenseKey);
                ResultTextBox.Clear();
            }
            catch (Exception ex)
            {
                StatusText.Text = "이미지를 불러오는 중 오류가 발생했습니다.";
                MessageBox.Show(this, ex.Message, "이미지 로드 실패", MessageBoxButton.OK, MessageBoxImage.Error);
                //AnalyzeButton.IsEnabled = false;
                _selectedImagePath = null;
                PreviewImage.Source = null;
            }
        }

        private async void OnAnalyzeClick(object sender, RoutedEventArgs e)
        {
            if (_selectedImagePath is null || !File.Exists(_selectedImagePath))
            {
                StatusText.Text = "분석할 이미지가 없습니다.";
                return;
            }

            if (string.IsNullOrWhiteSpace(_options.LicenseKey))
            {
                StatusText.Text = "라이선스 키를 먼저 적용하세요.";
                return;
            }

            AnalyzeButton.IsEnabled = false;
            ResultTextBox.Clear();
            StatusText.Text = "분석 중...";

            try
            {
                // CardAnalyzer 생성 시 라이선스 검증도 함께 수행된다.
                var analyzer = EnsureAnalyzer();
                CardAnalysisResult result = await analyzer.AnalyzeAsync(_selectedImagePath);
                var classification = DocumentClassifier.Classify(result);

                ResultTextBox.Text = classification.ToJson();
                StatusText.Text = classification.Result
                    ? $"분석 완료 - 문서 유형: {classification.Type}"
                    : "분석 완료 - 문서 유형 미확인";
            }
            catch (LicenseVerificationException ex)
            {
                StatusText.Text = $"라이선스 검증 실패: {ex.GetUserFriendlyMessage()}";
                MessageBox.Show(this, ex.Message, "라이선스 검증 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                StatusText.Text = "분석 실패";
                MessageBox.Show(this, ex.Message, "분석 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 이미지와 라이선스 키가 유효한 경우에만 버튼을 재활성화한다.
                AnalyzeButton.IsEnabled = _selectedImagePath is not null && !string.IsNullOrWhiteSpace(_options.LicenseKey);
            }
        }

        private void OnCopyJsonClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ResultTextBox.Text))
            {
                StatusText.Text = "복사할 JSON이 없습니다.";
                return;
            }

            // 분석 결과(JSON)를 클립보드로 복사해 외부에서 확인할 수 있도록 한다.
            Clipboard.SetText(ResultTextBox.Text);
            StatusText.Text = "JSON을 클립보드에 복사했습니다.";
        }

        private CardAnalyzer EnsureAnalyzer()
        {
            if (_analyzer is not null)
            {
                return _analyzer;
            }

            _analyzer = new CardAnalyzer(_options);
            return _analyzer;
        }

        private async void OnApplyLicenseClick(object sender, RoutedEventArgs e)
        {
            var key = LicenseKeyTextBox.Text?.Trim() ?? string.Empty;
            await ApplyLicenseKeyAsync(key, updateStatus: true);
        }

        private async Task ApplyLicenseKeyAsync(string key, bool updateStatus)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                if (updateStatus)
                {
                    StatusText.Text = "라이선스 키를 입력하세요.";
                }
                AnalyzeButton.IsEnabled = false;
                return;
            }

            AnalyzeButton.IsEnabled = false;
            StatusText.Text = "라이선스 검증 중...";

            var previousKey = _options.LicenseKey;
            _options.LicenseKey = key;

            try
            {
                // RestLicenseRegistry가 실제로 /license/verify API를 호출한다.
                await Task.Run(() =>
                {
                    using var tester = new CardAnalyzer(_options);
                });

                _analyzer?.Dispose();
                _analyzer = null;

                AnalyzeButton.IsEnabled = _selectedImagePath is not null;
                ResultTextBox.Clear();

                if (updateStatus)
                {
                    StatusText.Text = "라이선스 검증 성공. 분석을 실행할 수 있습니다.";
                }
            }
            catch (LicenseVerificationException ex)
            {
                _options.LicenseKey = previousKey;
                AnalyzeButton.IsEnabled = _selectedImagePath is not null && !string.IsNullOrWhiteSpace(previousKey);
                if (updateStatus)
                {
                    StatusText.Text = $"라이선스 검증 실패: {ex.GetUserFriendlyMessage()}";
                }
            }
            catch (Exception ex)
            {
                _options.LicenseKey = previousKey;
                AnalyzeButton.IsEnabled = _selectedImagePath is not null && !string.IsNullOrWhiteSpace(previousKey);
                if (updateStatus)
                {
                    StatusText.Text = $"라이선스 검증 실패: {ex.Message}";
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            // 창 종료 시 Analyzer 인스턴스를 정리한다.
            _analyzer?.Dispose();
            _analyzer = null;
        }
    }
}
