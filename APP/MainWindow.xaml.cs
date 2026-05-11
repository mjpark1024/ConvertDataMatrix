using APP.Properties;
using DataMatrixLib;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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


namespace APP
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        Bitmap m_BitLoadbmpimage;
        Bitmap m_BitConvertImage;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnLoadImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "이미지 파일|*.jpg;*.jpeg;*.png;*.bmp;*.gif";
            if (openFileDialog.ShowDialog() == true)
            {
                string diretoryPath = System.IO.Path.GetFullPath(openFileDialog.FileName);
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(diretoryPath); // 로컬 경로
                bitmap.EndInit();
                OriginImage.Source = bitmap;
                m_BitLoadbmpimage = new Bitmap(diretoryPath);
                Result.Text = "";
                ConvertImage.Source = null;
            }
        }

        //private void btnTest_Click(object sender, RoutedEventArgs e)
        //{
        //    string tessPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
        //    using (var engine = new TesseractEngine(tessPath, "kor+eng", EngineMode.Default))
        //    {
        //        using (var img = PixConverter.ToPix(Binarybmp))
        //        {
        //            using (var page = engine.Process(img))
        //            {
        //                string text = page.GetText();
        //                Result.Text = text;
        //            }
        //        }
        //    }
        //}
        private void btnConvert_Click(object sender, RoutedEventArgs e)
        {
            Mat srcImage = BitmapConverter.ToMat(m_BitLoadbmpimage);
            Mat m_ConvertImage = new Mat();
            double dResolution = Convert.ToDouble(Resolution.Text);
            var zxresult = DataMatrixConvert.Decode(srcImage, ref m_ConvertImage, dResolution);
            if (zxresult != null && zxresult.Trim() == "")
            {
                Result.Text = "Error";
            }
            else
            {
                Bitmap temp = BitmapConverter.ToBitmap(m_ConvertImage);
                ConvertImage.Source = ConvertBitmapToBitmapImage(temp);
                Result.Text = zxresult;
            }
        }
        public BitmapImage ConvertBitmapToBitmapImage(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                // 1. 비트맵을 메모리 스트림에 저장
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0; // 스트림 위치를 처음으로 되돌림

                // 2. BitmapImage 생성 및 초기화
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // 스트림 해제 후에도 이미지 사용 가능하게 설정
                bitmapImage.EndInit();

                // 3. Freeze를 호출하여 성능 향상 및 멀티스레드 이슈 방지
                bitmapImage.Freeze();

                return bitmapImage;
            }
        }
        private void btnMultiteste_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "이미지 파일|*.jpg;*.jpeg;*.png;*.bmp;*.gif";
            if (openFileDialog.ShowDialog() == true)
            {
                string diretoryPath = System.IO.Path.GetDirectoryName(openFileDialog.FileName);
                DirectoryInfo FolderPathInfo = new DirectoryInfo(diretoryPath);
                FileInfo[] Files = FolderPathInfo.GetFiles();
                if (Files.Length == 0) return;
                for (int i = 0; i < Files.Count(); i++)
                {
                    string str = Files[i].Name.ToLower();
                    m_BitLoadbmpimage = new Bitmap(diretoryPath + "\\"+ str);
                    List<string> id = new List<string>();
                    Mat srcImage = BitmapConverter.ToMat(m_BitLoadbmpimage);
                    Mat m_ConvertImage = new Mat();
                    double dResolution = Convert.ToDouble(Resolution.Text);
                    var zxresult = DataMatrixConvert.Decode(srcImage, ref m_ConvertImage, dResolution);
                    if (zxresult != null && zxresult.Trim() == "")
                    {
                        Result.Text = "Error";
                    }
                    else
                    {
                        Bitmap temp = BitmapConverter.ToBitmap(m_ConvertImage);
                        ConvertImage.Source = ConvertBitmapToBitmapImage(temp);
                        Result.Text = zxresult;
                    }
             
                    Thread.Sleep(500);
                    OriginImage.Source = null;
                    ConvertImage.Source = null;
                }
            }
        }        
    }
}
