using m3u8Downloader.MVVM;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace m3u8Downloader.ViewModel
{
    public class MainWindowViewModel : ViewModelBase
    {

        private int _url;

        public int Url
        {
            get { return _url; }
            set { _url = value; }
        }


        private string _videoPath;
        public string VideoPath
        {
            get => _videoPath;
            set { _videoPath = value; OnPropertyChanged(); }
        }

        private int _maxWorker = 5;
        public int MaxWorker
        {
            get => _maxWorker;
            set { _maxWorker = value; OnPropertyChanged(); }
        }

        private string _result;
        public string Result
        {
            get => _result;
            set { _result = value; OnPropertyChanged(); }
        }

        // Commands
        public ICommand DownloadCommand { get; }
        public ICommand CheckSizeCommand { get; }

        public MainWindowViewModel()
        {
            DownloadCommand = new RelayCommand(_ => Download());
            CheckSizeCommand = new RelayCommand(_ => CheckSize());
        }

        private void Download()
        {
            Result = $"Bắt đầu tải video từ: {Url}\nThư mục: {VideoPath}\nWorker: {MaxWorker}";
        }

        private void CheckSize()
        {
            Result = $"Đang kiểm tra size của video: {Url}";
        }

    }
}
