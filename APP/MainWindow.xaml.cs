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
            Mat Cvtimage = new Mat();
            Mat Gridimage = new Mat();
            double dResolution = Convert.ToDouble(Resolution.Text);
            var zxresult = DataMatrixConvert.Decode(srcImage, ref Cvtimage, ref Gridimage, dResolution);
            if (zxresult != null && zxresult.Trim() == "")
            {
                Result.Text = "Error";
            }
            else
            {
                Bitmap temp = BitmapConverter.ToBitmap(Gridimage);
                GridImage.Source = ConvertBitmapToBitmapImage(temp);
                temp = BitmapConverter.ToBitmap(Cvtimage);
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
        private async void btnMultiteste_Click(object sender, RoutedEventArgs e)
        {
            int ReadCount = 0;
            Percent.Content = "";
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "이미지 파일|*.jpg;*.jpeg;*.png;*.bmp;*.gif";
            if (openFileDialog.ShowDialog() == true)
            {
                string directoryPath = System.IO.Path.GetDirectoryName(openFileDialog.FileName);
                DirectoryInfo FolderPathInfo = new DirectoryInfo(directoryPath);
                FileInfo[] Files = FolderPathInfo.GetFiles()
                    .Where(f => f.Extension.ToLower() == ".jpg" ||
                                f.Extension.ToLower() == ".jpeg" ||
                                f.Extension.ToLower() == ".png" ||
                                f.Extension.ToLower() == ".bmp" ||
                                f.Extension.ToLower() == ".gif")
                    .ToArray();

                if (Files.Length == 0) return;

                foreach (var file in Files)
                {
                    string filePath = System.IO.Path.Combine(directoryPath, file.Name);
                    try
                    {
                        using (var bitmap = new Bitmap(filePath))
                        {
                            Bitmap BitLoadbmpimage = new Bitmap(bitmap); // 복사
                            OriginImage.Source = ConvertBitmapToBitmapImage(BitLoadbmpimage);
                            Mat srcImage = BitmapConverter.ToMat(BitLoadbmpimage);
                            Mat Cvtimage = new Mat();
                            Mat Gridimage = new Mat();
                            double dResolution = Convert.ToDouble(Resolution.Text);

                            var zxresult = DataMatrixConvert.Decode(srcImage, ref Cvtimage, ref Gridimage, dResolution);

                            if (zxresult != null && zxresult.Trim() != "")
                            {
                                Bitmap temp = BitmapConverter.ToBitmap(Gridimage);
                                GridImage.Source = ConvertBitmapToBitmapImage(temp);
                                temp = BitmapConverter.ToBitmap(Cvtimage);
                                ConvertImage.Source = ConvertBitmapToBitmapImage(temp);
                                Result.Text = zxresult;
                                ReadCount++;
                            }
                            else
                            {
                                Result.Text = "Error";
                            }

                            // UI 갱신 기회를 주고, 500ms 대기 (UI 스레드 블로킹 없음)
                            await Task.Delay(200);

                            // 다음 이미지를 위해 클리어
                            OriginImage.Source = null;
                            ConvertImage.Source = null;
                            GridImage.Source = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        Result.Text = "이미지 처리 중 오류: " + ex.Message;
                        await Task.Delay(500);
                    }

                }
                double dPercent = (double)ReadCount / (double)Files.Count() * 100;
                Percent.Content = dPercent.ToString();
            }
        }        
    }
}
