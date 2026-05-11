using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZXing;

namespace DataMatrixLib
{
    public class MultiMap<TKey, TValue> where TKey : IComparable<TKey>
    {
        private List<KeyValuePair<TKey, TValue>> data = new List<KeyValuePair<TKey, TValue>>();
        private bool sorted = false;

        public void Add(TKey key, TValue value)
        {
            data.Add(new KeyValuePair<TKey, TValue>(key, value));
            sorted = false;
        }

        private void SortIfNeeded()
        {
            if (sorted) return;

            data.Sort((a, b) =>
            {
                int cmp = a.Key.CompareTo(b.Key);
                if (cmp != 0) return cmp;
                return 0; // C++ multimap도 value sort 보장 없음
            });

            sorted = true;
        }

        public int Count(TKey key)
        {
            int c = 0;
            for (int i = 0; i < data.Count; i++)
                if (data[i].Key.CompareTo(key) == 0)
                    c++;
            return c;
        }

        public IEnumerable<TValue> GetValues(TKey key)
        {
            SortIfNeeded();

            for (int i = 0; i < data.Count; i++)
                if (data[i].Key.CompareTo(key) == 0)
                    yield return data[i].Value;
        }

        public void RemoveKey(TKey key)
        {
            data.RemoveAll(x => x.Key.CompareTo(key) == 0);
            sorted = false;
        }

        public bool Empty()
        {
            return data.Count == 0;
        }

        public IEnumerable<TKey> Keys
        {
            get
            {
                SortIfNeeded();

                TKey prev = default(TKey);
                bool first = true;

                for (int i = 0; i < data.Count; i++)
                {
                    if (first || data[i].Key.CompareTo(prev) != 0)
                    {
                        yield return data[i].Key;
                        prev = data[i].Key;
                        first = false;
                    }
                }
            }
        }
    }
    public static class DataMatrixConvert
    {
        static ZXing.IBarcodeReader zxing_reader = new BarcodeReader()
        {
            AutoRotate = true,
            TryInverted = true,
            Options = new ZXing.Common.DecodingOptions()
            {
                TryHarder = true,
                PossibleFormats = new List<BarcodeFormat>()
                {
                    BarcodeFormat.DATA_MATRIX
                }
            }
        };
        static SortedDictionary<float, int> mapPeakInfo;
        public static string Decode(Mat srcImage, ref Mat DstImg, ref Mat CvtImg, double Res, int simbol = 0, int ithreshold = -1)
        {
            try
            {
                Mat image = srcImage.Clone();
                List<string> codes = new List<string>();
                Mat label_box = new Mat();

                Mat image_gray = new Mat();
                Mat image_bi = new Mat();
                // 라벨 레이어 변수
                Mat img_label = new Mat();
                Mat stats = new Mat();

                Mat centroids = new Mat();
                Mat mask = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3), new Point(1, 1));

                int label;
                // 복사
                label_box = image.Clone();

                //그레이스케일
                if (image.Channels() > 1)
                    Cv2.CvtColor(image, image_gray, ColorConversionCodes.BGR2GRAY);
                else
                    image.CopyTo(image_gray);

                Mat MatRoiImg = new Mat();
                Mat tempImg = new Mat();
                Mat MCRResultImg;
                Mat MatRoiImgBi = new Mat(); ;
                Mat MatRoiImgBiLine = new Mat();
                Mat MatRoiImgDot = new Mat();
                Mat CrossLineImg = new Mat();

                int iboraderLength = 0;
                List<Mat> ListMatRoiImg = new List<Mat>();
                List<Rect> ListFindRect = new List<Rect>();
                bool bFind = false;
                bool bReverse = false;
                List<bool> ListReverseFlag = new List<bool>();

                bool bRetry = false;
                bool bFindFlag = true;
                ListMatRoiImg.Clear();
                bool AutoSizeChk = true;
                int iMatrixCnt = 16;
                int MinSizeFilter = Convert.ToInt32(1600 / Res);

                for (int i = 0; i < 5; i++)
                {
                    for (int z = 0; z < 10; z++)
                    {
                        if (i == 0) //이진화
                        {
                            if (z == 0)
                            {
                                if (ithreshold != -1)
                                    Cv2.Threshold(image_gray, image_bi, ithreshold, 255, ThresholdTypes.Binary);
                                else//OTSU 알고리즘
                                    Cv2.Threshold(image_gray, image_bi, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
                                Cv2.Dilate(image_bi, image_bi, mask, new Point(-1, -1), 1, BorderTypes.Replicate); //노이즈 제거
                                Cv2.Erode(image_bi, image_bi, mask, new Point(-1, -1), 1, BorderTypes.Replicate); //노이즈 제거
                            }
                            else
                                Cv2.Dilate(image_bi, image_bi, mask, new Point(1, 1), z, BorderTypes.Replicate); //노이즈 제거      
                        }
                        else if (i == 1) //(첫번째 이진화 실패할 경우 반전)
                        {
                            bReverse = true;
                            if (z == 0)
                            {
                                if (ithreshold != -1)
                                    Cv2.Threshold(image_gray, image_bi, ithreshold, 255, ThresholdTypes.BinaryInv);
                                else//OTSU 알고리즘
                                    Cv2.Threshold(image_gray, image_bi, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
                                Cv2.Erode(image_bi, image_bi, mask, new Point(-1, -1), 1, BorderTypes.Replicate); //노이즈 제거  
                                Cv2.Dilate(image_bi, image_bi, mask, new Point(-1, -1), 1, BorderTypes.Replicate); //노이즈 제거       
                            }
                            else
                                Cv2.Erode(image_bi, image_bi, mask, new Point(1, 1), z, BorderTypes.Replicate); //노이즈 제거      
                        }
                        else if (i == 2) //(첫번째 이진화 실패할 경우 반전)
                        {
                            bReverse = false;
                            if (z == 0)
                            {
                                Cv2.Threshold(image_gray, image_bi, 150, 255, ThresholdTypes.Binary);
                                Cv2.Dilate(image_bi, image_bi, mask, new Point(-1, -1), 1, BorderTypes.Replicate); //노이즈 제거
                                Cv2.Erode(image_bi, image_bi, mask, new Point(-1, -1), 1, BorderTypes.Replicate); //노이즈 제거
                            }
                            else
                                Cv2.Dilate(image_bi, image_bi, mask, new Point(1, 1), z, BorderTypes.Replicate); //노이즈 제거      
                        }

                        else if (i == 3) //(첫번째 이진화 실패할 경우 반전)
                        {
                            bReverse = true;
                            if (z == 0)
                            {
                                Cv2.Threshold(image_gray, image_bi, 150, 255, ThresholdTypes.BinaryInv);
                                Cv2.Erode(image_bi, image_bi, mask, new Point(-1, -1), 1, BorderTypes.Replicate); //노이즈 제거    
                                Cv2.Dilate(image_bi, image_bi, mask, new Point(-1, -1), 1, BorderTypes.Replicate); //노이즈 제거       
                            }
                            else
                                Cv2.Erode(image_bi, image_bi, mask, new Point(1, 1), z, BorderTypes.Replicate); //노이즈 제거      
                        }
                        else if (i == 4) //(첫번째 이진화 실패할 경우 반전)
                        {
                            bReverse = false;
                            if (z == 0)
                            {
                                Cv2.GaussianBlur(image_gray, image_gray, new Size(3, 3), 0);
                                Cv2.Canny(image_gray, image_bi, 0, 50, 3);
                            }
                            else
                                Cv2.Dilate(image_bi, image_bi, mask, new Point(1, 1), z, BorderTypes.Replicate); //노이즈 제거   
                        }

                        Mat cent = new Mat();
                        label = Cv2.ConnectedComponentsWithStats(image_bi, img_label, stats, centroids, PixelConnectivity.Connectivity8, MatType.CV_32S);

                        int iCutSize = 1;
                        //if (!AutoSizeChk) iCutSize = 0;

                        int area, left, top, width, height;
                        for (int j = 1; j < label; j++)
                        {
                            left = stats.At<int>(j, (int)ConnectedComponentsTypes.Left);
                            top = stats.At<int>(j, (int)ConnectedComponentsTypes.Top);
                            width = stats.At<int>(j, (int)ConnectedComponentsTypes.Width);
                            height = stats.At<int>(j, (int)ConnectedComponentsTypes.Height);
                            area = stats.At<int>(j, (int)ConnectedComponentsTypes.Area);

                            // 찾은 덩어리 중 큰것부터 작은 것 순으로 검색하여. 정사각형과 유사하고, 사이즈가 적당히 큰 것으로 리턴한다. 큰 사각형 테두리를 찾더라도 더 작은 것이 우선
                            if ((Math.Abs(width - height) < 20 || (width > height * 1.3 && width < height * 3) || (height > width * 1.3 && height < width * 3)) && width > MinSizeFilter && height > MinSizeFilter)
                            {
                                // 라벨링 박스
                                if (left < iCutSize || top < iCutSize || image.Cols < left + width + (iCutSize) || image.Rows < top + height + (iCutSize)) continue;

                                Cv2.Rectangle(label_box, new Rect(left - iCutSize, top - iCutSize, width + (iCutSize * 2), height + (iCutSize * 2)), new Scalar(0, 0, 255), 3);

                                Rect roi = new Rect(left - iCutSize, top - iCutSize, width + (iCutSize * 2), height + (iCutSize * 2));
                                MatRoiImg = image_gray.SubMat(roi);
                                Rect biroi = new Rect(left - iCutSize, top - iCutSize, width + (iCutSize * 2), height + (iCutSize * 2));
                                tempImg = image_bi.SubMat(biroi);

                                int Size = 0;
                                FindCountour(tempImg, ref Size);
                                if (Size < 15)//찾은 뭉티기 개수가 15개 미만(MCR Image를 제대로 커트했으면 15개 이상 나옴)
                                {
                                    continue;
                                }
                                ListMatRoiImg.Add(MatRoiImg);
                                ListFindRect.Add(new Rect(new Point(top, left), new Size(height, width)));
                                ListReverseFlag.Add(bReverse);
                                bFind = true;
                            }
                        }
                        if (bFind && i != 0) break;
                    }
                    if (bFind && i != 0) break;
                }


                if (ListMatRoiImg.Count() == 0)
                    bFindFlag = false;

                for (int k = 0; k < ListMatRoiImg.Count(); k++) //찾은 후보들을 전부 Search
                {
                    if (ListMatRoiImg == null)
                    {
                        bFindFlag = false;
                        break;
                    }
                    if (ListMatRoiImg[k].Rows < 100)// 100pixel 미만인것들은 Resize 한다.
                    {
                        Cv2.Resize(ListMatRoiImg[k], ListMatRoiImg[k], new Size(), 3.0, 3.0, InterpolationFlags.Lanczos4);
                    }
                    else
                    {
                        if (!bRetry)//후보의 노이즈 제거
                        {
                            if (ListReverseFlag[k])
                            {
                                Cv2.Erode(ListMatRoiImg[k], ListMatRoiImg[k], mask, new Point(-1, -1), 1, BorderTypes.Replicate);
                                Cv2.Dilate(ListMatRoiImg[k], ListMatRoiImg[k], mask, new Point(-1, -1), 1, BorderTypes.Replicate);

                            }
                            else
                            {
                                Cv2.Dilate(ListMatRoiImg[k], ListMatRoiImg[k], mask, new Point(-1, -1), 1, BorderTypes.Replicate);
                                Cv2.Erode(ListMatRoiImg[k], ListMatRoiImg[k], mask, new Point(-1, -1), 1, BorderTypes.Replicate);
                            }
                        }
                        else
                        {
                            if (ListReverseFlag[k])
                            {
                                Cv2.Dilate(ListMatRoiImg[k], ListMatRoiImg[k], mask, new Point(-1, -1), 1, BorderTypes.Replicate);
                                Cv2.Erode(ListMatRoiImg[k], ListMatRoiImg[k], mask, new Point(-1, -1), 1, BorderTypes.Replicate);
                            }
                            else
                            {
                                Cv2.Erode(ListMatRoiImg[k], ListMatRoiImg[k], mask, new Point(-1, -1), 1, BorderTypes.Replicate);
                                Cv2.Dilate(ListMatRoiImg[k], ListMatRoiImg[k], mask, new Point(-1, -1), 1, BorderTypes.Replicate);
                            }
                        }
                    }
    
                    for (int z = 0; z < 10; z++)
                    {
                        bFindFlag = true;
                        int iMeanPeak = 0, iPeakCnt = 0;
                        mapPeakInfo = new SortedDictionary<float, int>();
                        if (ithreshold != -1)//찾은 후보를 고정 threshold로 변환
                        {
                            Cv2.Threshold(ListMatRoiImg[k], MatRoiImgBi, ithreshold, 255, ThresholdTypes.Binary);
                            iPeakCnt = -1;
                        }
                        else//찾은 후보를 히스토그램을 구해 가장 높은 Peak 와 그 다음 Peaak의 평균 값으로 threshold
                        {
                            FindHistMeanPeak(ListMatRoiImg[k], out iMeanPeak);
                            Cv2.Threshold(ListMatRoiImg[k], MatRoiImgBi, iMeanPeak, 255, ThresholdTypes.Binary);
                        }
                        if (z != 0) Cv2.Dilate(MatRoiImgBi, MatRoiImgBi, mask, new Point(1, 1), z, BorderTypes.Replicate); //마킹 확산
                        iboraderLength = (int)(MatRoiImgBi.Rows / 100); //가장자리에 여백 만들기(여백이 어느정도 있어야함)
                        if (!bRetry)
                        {
                            if (ListReverseFlag[k])
                            {
                                Cv2.CopyMakeBorder(MatRoiImgBi, MatRoiImgBi, iboraderLength, iboraderLength, iboraderLength, iboraderLength, BorderTypes.Constant, 255);
                                Cv2.Dilate(MatRoiImgBi, MatRoiImgBi, mask, new Point(-1, -1), iboraderLength, BorderTypes.Replicate);
                                Cv2.Erode(MatRoiImgBi, MatRoiImgBi, mask, new Point(-1, -1), iboraderLength, BorderTypes.Replicate);
                            }
                            else
                            {
                                Cv2.CopyMakeBorder(MatRoiImgBi, MatRoiImgBi, iboraderLength, iboraderLength, iboraderLength, iboraderLength, BorderTypes.Constant, 0);
                                Cv2.Erode(MatRoiImgBi, MatRoiImgBi, mask, new Point(-1, -1), iboraderLength, BorderTypes.Replicate);
                                Cv2.Dilate(MatRoiImgBi, MatRoiImgBi, mask, new Point(-1, -1), iboraderLength, BorderTypes.Replicate);
                            }
                        }
                        else
                        {
                            if (ListReverseFlag[k])
                            {
                                Cv2.CopyMakeBorder(MatRoiImgBi, MatRoiImgBi, iboraderLength, iboraderLength, iboraderLength, iboraderLength, BorderTypes.Constant, 0);
                                Cv2.Erode(MatRoiImgBi, MatRoiImgBi, mask, new Point(-1, -1), iboraderLength, BorderTypes.Replicate);
                                Cv2.Dilate(MatRoiImgBi, MatRoiImgBi, mask, new Point(-1, -1), iboraderLength, BorderTypes.Replicate);
                            }
                            else
                            {
                                Cv2.CopyMakeBorder(MatRoiImgBi, MatRoiImgBi, iboraderLength, iboraderLength, iboraderLength, iboraderLength, BorderTypes.Constant, 255);
                                Cv2.Dilate(MatRoiImgBi, MatRoiImgBi, mask, new Point(-1, -1), iboraderLength, BorderTypes.Replicate);
                                Cv2.Erode(MatRoiImgBi, MatRoiImgBi, mask, new Point(-1, -1), iboraderLength, BorderTypes.Replicate);
                            }
                        }
                        int[] iindexChk = new int[2];
                        List<int>[] ListEdgePoints = new List<int>[2];
                        for (int i = 0; i < 2; i++) ListEdgePoints[i] = new List<int>();
                        bool[] m_bMCROrigin = new bool[2];
                        int m_iMCROrigin = 0;
                        if (AutoSizeChk)
                        {
                            iMatrixCnt = 0;
                            int iCntTmp = simbol;
                            for (int rotate = 0; rotate < 2; rotate++) //상면, 하면 검사하여 Matrix Dot Count Check
                            {
                                while (OriginMake(MatRoiImgBi.Clone()) ? !FindMCRCnt(MatRoiImgBi, rotate, ref ListEdgePoints, ref iindexChk, ref iCntTmp, ref m_bMCROrigin, ref m_iMCROrigin) : true)
                                {
                                    iPeakCnt++;
                                    if (!FindHistMeanPeak(ListMatRoiImg[k], out iMeanPeak, iPeakCnt) || iPeakCnt > 5)
                                    {
                                        bFindFlag = false;
                                        break;
                                    }

                                    iCntTmp = simbol;
                                    rotate = 0;
                                    Cv2.Threshold(ListMatRoiImg[k], MatRoiImgBi, iMeanPeak, 255, ThresholdTypes.Binary);
                                    if (!bRetry)
                                    {
                                        if (ListReverseFlag[k])
                                        {
                                            Cv2.CopyMakeBorder(MatRoiImgBi, MatRoiImgBi, iboraderLength, iboraderLength, iboraderLength, iboraderLength, BorderTypes.Constant, 255);
                                            Cv2.Dilate(MatRoiImgBi, MatRoiImgBi, mask, new Point(-1, -1), iboraderLength, BorderTypes.Replicate);
                                            Cv2.Erode(MatRoiImgBi, MatRoiImgBi, mask, new Point(-1, -1), iboraderLength, BorderTypes.Replicate);
                                        }
                                        else
                                        {
                                            Cv2.CopyMakeBorder(MatRoiImgBi, MatRoiImgBi, iboraderLength, iboraderLength, iboraderLength, iboraderLength, BorderTypes.Constant, 0);
                                            Cv2.Erode(MatRoiImgBi, MatRoiImgBi, mask, new Point(-1, -1), iboraderLength, BorderTypes.Replicate);
                                            Cv2.Dilate(MatRoiImgBi, MatRoiImgBi, mask, new Point(-1, -1), iboraderLength, BorderTypes.Replicate);
                                        }
                                    }
                                    else
                                    {
                                        if (ListReverseFlag[k])
                                        {
                                            Cv2.CopyMakeBorder(MatRoiImgBi, MatRoiImgBi, iboraderLength, iboraderLength, iboraderLength, iboraderLength, BorderTypes.Constant, 0);
                                            Cv2.Erode(MatRoiImgBi, MatRoiImgBi, mask, new Point(-1, -1), iboraderLength, BorderTypes.Replicate);
                                            Cv2.Dilate(MatRoiImgBi, MatRoiImgBi, mask, new Point(-1, -1), iboraderLength, BorderTypes.Replicate);
                                        }
                                        else
                                        {
                                            Cv2.CopyMakeBorder(MatRoiImgBi, MatRoiImgBi, iboraderLength, iboraderLength, iboraderLength, iboraderLength, BorderTypes.Constant, 255);
                                            Cv2.Dilate(MatRoiImgBi, MatRoiImgBi, mask, new Point(-1, -1), iboraderLength, BorderTypes.Replicate);
                                            Cv2.Erode(MatRoiImgBi, MatRoiImgBi, mask, new Point(-1, -1), iboraderLength, BorderTypes.Replicate);
                                        }
                                    }
                                }

                                if (iCntTmp >= iMatrixCnt)
                                    iMatrixCnt = iCntTmp;
                                if (bFindFlag == false && bRetry == true) continue;

                                if (iMatrixCnt < 10)
                                {
                                    continue;
                                }
                            }

                            if (iMatrixCnt < 10 ||
                                (ListEdgePoints[0] == null || ListEdgePoints[0].Count == 0) ||
                                (ListEdgePoints[1] == null || ListEdgePoints[1].Count == 0) ||
                                ListEdgePoints[0].Count() % 2 == 1 ||
                                ListEdgePoints[1].Count() % 2 == 1)// ||
                                                                   //ListEdgePoints[0].Count() != ListEdgePoints[1].Count())
                            {
                                bFindFlag = false;
                            }

                            if (bFindFlag == true)
                            {
                                /////////////////////검증용/////////////////////////
                                MatRoiImgBi.CopyTo(MatRoiImgBiLine);
                                MatRoiImgBi.CopyTo(MatRoiImgDot);
                                //Cv2.CvtColor(ListMatRoiImg[k], ListMatRoiImg[k], ColorConversionCodes.GRAY2BGR);
                                Cv2.CvtColor(MatRoiImgBiLine, MatRoiImgBiLine, ColorConversionCodes.GRAY2BGR);
                                //라인 search
                                for (int rotate = 0; rotate < ListEdgePoints[0].Count; rotate++) //검증용 Line 그리기
                                {
                                    Cv2.Line(MatRoiImgBiLine, ListEdgePoints[0][rotate], 0, ListEdgePoints[0][rotate], MatRoiImgBi.Rows, new Scalar(0, 255, 0));
                                }
                                for (int rotate = 0; rotate < ListEdgePoints[1].Count; rotate++)
                                {
                                    Cv2.Line(MatRoiImgBiLine, 0, ListEdgePoints[1][rotate], MatRoiImgBi.Cols, ListEdgePoints[1][rotate], new Scalar(0, 255, 0));
                                }
                                MatRoiImgBiLine.CopyTo(CrossLineImg);

                                int numerator, denominator, multColum, multRow;
                                if (ListEdgePoints[0].Count > ListEdgePoints[1].Count)
                                {
                                    numerator = ListEdgePoints[0].Count;
                                    denominator = ListEdgePoints[1].Count;
                                    multColum = (int)Math.Ceiling((double)(numerator / denominator));
                                    multRow = 1;

                                }
                                else
                                {
                                    numerator = ListEdgePoints[1].Count;
                                    denominator = ListEdgePoints[0].Count;
                                    multColum = 1;
                                    multRow = (int)Math.Ceiling((double)(numerator / denominator));
                                }
                                CvtImg = CrossLineImg;
                                Cv2.Resize(CrossLineImg, CrossLineImg, new Size(300 * multColum, 300 * multRow), 0, 0, InterpolationFlags.Linear);
                                //////////////////////////////////////////////////////
                                //MatRoiImgBiLine                        
                                if (!bRetry)
                                {
                                    if (ListReverseFlag[k])
                                        MCRResultImg = new Mat(ListEdgePoints[1].Count + 3, ListEdgePoints[0].Count + 3, MatType.CV_8U, new Scalar(255));
                                    else
                                        MCRResultImg = new Mat(ListEdgePoints[1].Count + 3, ListEdgePoints[0].Count + 3, MatType.CV_8U, new Scalar(0));
                                }
                                else
                                {
                                    if (!ListReverseFlag[k])
                                        MCRResultImg = new Mat(ListEdgePoints[1].Count + 3, ListEdgePoints[0].Count + 3, MatType.CV_8U, new Scalar(255));
                                    else
                                        MCRResultImg = new Mat(ListEdgePoints[1].Count + 3, ListEdgePoints[0].Count + 3, MatType.CV_8U, new Scalar(0));
                                }
                                unsafe
                                {
                                    int ix, iy;
                                    //바이너리  IMAGE 생성 
                                    Mat MCRResult = MCRResultImg.SubMat(new Rect(1, 1, ListEdgePoints[0].Count + 1, ListEdgePoints[1].Count + 1));

                                    for (int rotate = 0; rotate <= ListEdgePoints[1].Count; rotate++)
                                    {
                                        if (rotate == 0)
                                        {
                                            if (m_iMCROrigin == 1 || m_iMCROrigin == 2)
                                                continue;

                                            ix = (int)((ListEdgePoints[1][rotate] / 2) + 0.5);
                                        }
                                        else if (rotate == ListEdgePoints[1].Count)
                                        {
                                            if (m_iMCROrigin == 3 || m_iMCROrigin == 4)
                                                continue;

                                            ix = (int)((MatRoiImgBi.Rows + ListEdgePoints[1][rotate - 1]) / 2 - 0.5);
                                        }
                                        else
                                        {
                                            if (m_iMCROrigin == 1 || m_iMCROrigin == 2)
                                                ix = (int)((ListEdgePoints[1][rotate - 1] + ListEdgePoints[1][rotate]) / 2 - 0.5);
                                            else
                                                ix = (int)((ListEdgePoints[1][rotate - 1] + ListEdgePoints[1][rotate]) / 2 + 0.5);
                                        }

                                        byte* dotPixel = (byte*)MatRoiImgDot.Ptr(ix);
                                        byte* mcrPtr = (byte*)MCRResult.Ptr(rotate);

                                        for (int j = 0; j <= ListEdgePoints[0].Count; j++)
                                        {
                                            if (j == 0)
                                            {
                                                if (m_iMCROrigin == 1 || m_iMCROrigin == 3)
                                                    continue;

                                                iy = (int)((ListEdgePoints[0][j] / 2) + 0.5);
                                            }
                                            else if (j == ListEdgePoints[0].Count)
                                            {
                                                if (m_iMCROrigin == 2 || m_iMCROrigin == 4)
                                                    continue;

                                                iy = (int)((MatRoiImgBi.Cols + ListEdgePoints[0][j - 1]) / 2 + 0.5);
                                            }
                                            else
                                            {
                                                iy = (int)((ListEdgePoints[0][j - 1] + ListEdgePoints[0][j]) / 2 + 0.5);
                                            }

                                            mcrPtr[j] = dotPixel[iy];
                                            dotPixel[iy] = 127;
                                        }
                                    }
                                    //가장자리 영역 재구성 (X x X)
                                    //상                            
                                    for (int rotate = 1; rotate < MCRResult.Cols; rotate++)
                                    {
                                        if (m_iMCROrigin == 1 || m_iMCROrigin == 2) break;
                                        byte value = MCRResult.At<byte>(0, rotate - 1);
                                        value = (value == 255) ? (byte)0 : (byte)255;
                                        MCRResult.Set(0, rotate, value);
                                    }
                                    //하
                                    for (int rotate = 1; rotate < MCRResult.Cols; rotate++)
                                    {
                                        if (m_iMCROrigin == 3 || m_iMCROrigin == 4) break;
                                        byte value = MCRResult.At<byte>(MCRResult.Rows - 1, rotate - 1);
                                        MCRResult.Set(MCRResult.Rows - 1, rotate, (value == 255) ? (byte)0 : (byte)255);
                                    }
                                    //좌
                                    for (int rotate = 1; rotate < MCRResult.Rows; rotate++)
                                    {
                                        if (m_iMCROrigin == 1 || m_iMCROrigin == 3) break;
                                        byte value = MCRResult.At<byte>(rotate - 1, 0);
                                        MCRResult.Set(rotate, 0, (value == 255) ? (byte)0 : (byte)255);
                                    }
                                    //우
                                    for (int rotate = 1; rotate < MCRResult.Rows; rotate++)
                                    {
                                        if (m_iMCROrigin == 2 || m_iMCROrigin == 4) break;
                                        byte value = MCRResult.At<byte>(rotate - 1, MCRResult.Cols - 1);
                                        MCRResult.Set(rotate, MCRResult.Cols - 1, (value == 255) ? (byte)0 : (byte)255);
                                    }

                                    switch (m_iMCROrigin)
                                    {
                                        case 1:
                                            MCRResultImg = new Mat(MCRResultImg, new Rect(1, 1, MCRResultImg.Cols - 1, MCRResultImg.Rows - 1));
                                            break;
                                        case 2:
                                            MCRResultImg = new Mat(MCRResultImg, new Rect(0, 1, MCRResultImg.Cols - 1, MCRResultImg.Rows - 1));
                                            break;
                                        case 3:
                                            MCRResultImg = new Mat(MCRResultImg, new Rect(1, 0, MCRResultImg.Cols - 1, MCRResultImg.Rows - 1));
                                            break;
                                        case 4:
                                            MCRResultImg = new Mat(MCRResultImg, new Rect(0, 0, MCRResultImg.Cols - 1, MCRResultImg.Rows - 1));
                                            break;
                                        default:
                                            MCRResultImg = new Mat(MCRResultImg, new Rect(1, 0, MCRResultImg.Cols - 1, MCRResultImg.Rows - 1));
                                            break;
                                    }
                                }
                                Cv2.Resize(MCRResultImg, MCRResultImg, new Size(MCRResultImg.Cols * 10, MCRResultImg.Rows * 10), 0, 0, InterpolationFlags.Nearest);
                                MCRResultImg.CopyTo(DstImg);
                            }
                        }
                        if (bFindFlag)
                        {
                            var result = RecognitionMatrix(DstImg);
                            if (result == "")
                            {
                                bFindFlag = false; break;
                            }
                            return result;
                        }
                    }

                    if (bFindFlag)
                    {
                        return "";
                    }

                    else
                    {
                        if (k == ListMatRoiImg.Count - 1 && bRetry == false)
                        {
                            bRetry = true;
                            k = -1;
                        }
                        continue;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return "";
            }
            return "";
        }
        static void FindCountour(Mat img, ref int iSize)
        {
            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            Mat gray = new Mat();
            img.CopyTo(gray);

            // 방법 1 : 캐니 에지.    주의 : 에지 처리를 하면 두개의 윤곽선이 나오게 된다.
            Cv2.Canny(gray, gray, 200, 300, 3); //cv::namedWindow("edge Image");	cv::imshow("edge Image", gray);

            Point[][] contours;// 윤곽선 한개는 vector<Point> 로 충분. 한 화면에 윤곽선이 많기 때문에 이들의 벡터 표현으로 윤곽선 집합이 표현된다.
            HierarchyIndex[] hierarchy;

            RNG rng = new RNG(12345);

            Cv2.FindContours(gray,  // Source, an 8-bit single-channel image. Non-zero pixels are treated as 1’s. 
                out contours,       // output. Detected contours. Each contour is stored as a vector of points.
                out hierarchy,

                 //CV_RETR_EXTERNAL,	// retrieves only the extreme outer contours. It sets hierarchy[i][2]=hierarchy[i][3]=-1 for all the contours.
                 RetrievalModes.Tree,       // retrieves all of the contours and reconstructs a full hierarchy of nested contours.
                                            //CV_RETR_CCOMP,		// retrieves all of the contours and organizes them into a two-level hierarchy

                ContourApproximationModes.ApproxSimple // Contour approximation method.  compresses horizontal, vertical, and diagonal segments and leaves only their end points.
              );       // Optional offset by which every contour point is shifted. This is useful if the contours are extracted from the image ROI and then they should be analyzed in the whole image context.

            // Draw contours 렉트 확인용
            iSize = contours.Count();            
        }
        static bool FindHistMeanPeak(Mat img, out int iMeanPeak, int repeatcnt = 0, int iSensitive = 7)
        {
            iMeanPeak = 0;

            // 히스토그램 피크 구하기
            if (img.Empty())
                return false;

            int[] iFisrtSecondPeak = new int[2];

            if (repeatcnt != 0)
            {
                var keys = mapPeakInfo.Keys.Reverse().ToList();

                if (keys.Count <= repeatcnt + 1)
                    return false;

                iFisrtSecondPeak[0] = mapPeakInfo[keys[0]];
                iFisrtSecondPeak[1] = mapPeakInfo[keys[repeatcnt + 1]];

                iMeanPeak = (iFisrtSecondPeak[0] + iFisrtSecondPeak[1]) / 2;
            }
            else
            {
                Size szHistImg = new Size(512 * 2, 300 * 2);
                double textScale = 1.0;
                Size divNum = new Size(10, 10);
                int guideLineClr = 150;
                int backClr = 1;
                bool bFill = false;
                mapPeakInfo.Clear();

                // 히스토그램 계산
                int histSize = 256;
                Rangef histRange = new Rangef(0, histSize);
                Mat b_hist = new Mat();

                Cv2.CalcHist(new Mat[] { img }, new int[] { 0 }, null, b_hist, 1, new int[] { histSize }, new Rangef[] { histRange }, true, false);

                // 히스토그램 정규화
                Mat selectedHist = b_hist;

                double maxVal = 0, minVal = 0;
                Cv2.MinMaxLoc(selectedHist, out minVal, out maxVal);

                // presetting
                Scalar bgClr = new Scalar(255, 255, 255);
                Scalar bkClr = new Scalar(0, 0, 0);
                Scalar grClr = new Scalar(guideLineClr, guideLineClr, guideLineClr);
                if (backClr == 1)
                {
                    bkClr = new Scalar(255, 255, 255);
                    bgClr = new Scalar(0, 0, 0);
                }

                int temp = 0;
                Size szTextHor = Cv2.GetTextSize("000", HersheyFonts.HersheyPlain, textScale, 1, out temp);
                Size szTextMaxVal = Cv2.GetTextSize(((int)maxVal).ToString(), HersheyFonts.HersheyPlain, textScale, 1, out temp);

                int marginText = 15;
                int marginBtm = szTextHor.Height + marginText * 2;
                int marginRig = 30;
                int marginLef = szTextMaxVal.Width + marginText * 2;
                int marginTop = 30;

                int hist_w = szHistImg.Width;
                int hist_h = szHistImg.Height;

                float bin_w = (float)(hist_w - marginLef - marginRig) / (float)histSize;

                Mat MaskedHist = new Mat();

                Mat histImage = new Mat(hist_h, hist_w, MatType.CV_8UC3, bgClr);

                // Draw frame
                Cv2.ArrowedLine(histImage, new Point(marginLef, hist_h - marginBtm), new Point(hist_w - marginRig * 0.5, hist_h - marginBtm), bkClr, 2, LineTypes.Link8, 0, 0.008);   // bottom line
                Cv2.ArrowedLine(histImage, new Point(marginLef, hist_h - marginBtm), new Point(marginLef, marginTop * 0.5), bkClr, 2, LineTypes.Link8, 0, 0.015); // left

                int repValNumHori = divNum.Width;
                int interHori = Convert.ToInt32((double)((hist_w - marginLef - marginRig) / repValNumHori));
                float interValHori = (float)(histSize - 1) / (float)repValNumHori;
                for (int ii = 0; ii < repValNumHori + 1; ii++)
                {
                    string textHori = ((int)(interValHori * ii)).ToString();
                    Size szTextHori = Cv2.GetTextSize(textHori, HersheyFonts.HersheyPlain, textScale, 1, out temp);
                    Cv2.PutText(histImage, textHori, new Point(marginLef + interHori * ii - szTextHori.Width * 0.5, hist_h - marginBtm + szTextHor.Height + marginText), HersheyFonts.HersheyPlain, textScale, bkClr, 1, LineTypes.Link8, false);
                }

                // Draw vertical value
                int repValNumVert = divNum.Height;
                int interVert = Convert.ToInt32((hist_h - marginTop - marginBtm) / repValNumVert);
                float interValVert = (float)(maxVal) / (float)repValNumVert;
                for (int ii = 0; ii < repValNumVert; ii++)
                {
                    string textVer = ((int)(interValVert * (repValNumVert - ii))).ToString();
                    Size szTextVer = Cv2.GetTextSize(textVer, HersheyFonts.HersheyPlain, textScale, 1, out temp);
                    int rightAlig = szTextMaxVal.Width - szTextVer.Width;
                    Cv2.PutText(histImage, textVer, new Point(marginLef - szTextMaxVal.Width + rightAlig - marginText, marginTop + interVert * ii + szTextVer.Height * 0.5), HersheyFonts.HersheyPlain, textScale, bkClr, 1, LineTypes.Link8, false);
                }

                // Draw horizontal guide line
                for (int ii = 1; ii <= repValNumHori; ii++)
                {
                    Cv2.Line(histImage, Convert.ToInt32(marginLef + interHori * ii), Convert.ToInt32(marginTop * 0.5), Convert.ToInt32(marginLef + interHori * ii), Convert.ToInt32(hist_h - marginBtm), grClr, 1, LineTypes.Link8, 0);
                    Cv2.Line(histImage, Convert.ToInt32(marginLef + interHori * ii), Convert.ToInt32(hist_h - marginBtm), Convert.ToInt32(marginLef + interHori * ii), Convert.ToInt32(hist_h - marginBtm + 10), bkClr, 1, LineTypes.Link8, 0);
                }

                // Draw vertical guide line
                for (int ii = 0; ii < repValNumVert; ii++)
                {
                    Cv2.Line(histImage, Convert.ToInt32(marginLef - 10), Convert.ToInt32(marginTop + interVert * ii), Convert.ToInt32(hist_w - marginRig), Convert.ToInt32(marginTop + interVert * ii), grClr, 1, LineTypes.Link8, 0);
                    Cv2.Line(histImage, Convert.ToInt32(marginLef - 10), Convert.ToInt32(marginTop + interVert * ii), Convert.ToInt32(marginLef), Convert.ToInt32(marginTop + interVert * ii), bkClr, 1, LineTypes.Link8, 0);
                }
                /// Normalize the result to [ 0, histImage.rows-margin of top & bottom ]
                Cv2.Normalize(selectedHist, selectedHist, 0, histImage.Rows - marginTop - marginBtm, NormTypes.MinMax, -1);
                selectedHist.CopyTo(MaskedHist);
                //blur(selectedHist, MaskedHist, Size(1,20));
                //cv::medianBlur(selectedHist, MaskedHist, 3);
                Cv2.GaussianBlur(selectedHist, MaskedHist, new Size(iSensitive, iSensitive), 0);
                /// Draw for each channel
                if (!bFill)
                {
                    for (int i = 1; i < histSize; i++)
                    {
                        Cv2.Line(histImage, Convert.ToInt32(marginLef + (bin_w * (i - 1))), Convert.ToInt32(hist_h - marginBtm - MaskedHist.At<float>(i - 1)), Convert.ToInt32(marginLef + (bin_w * (i))), Convert.ToInt32((hist_h - marginBtm - MaskedHist.At<float>(i))), bkClr, 2, LineTypes.Link8, 0);
                    }
                }

                else if (bFill)
                {
                    for (int i = 1; i < histSize; i++)
                    {
                        Point prePtR = new Point(Convert.ToInt32(marginLef + (bin_w * (i - 1))), Convert.ToInt32(hist_h - marginBtm));
                        Point postPtR = new Point(Convert.ToInt32(marginLef + (bin_w * (i))), Convert.ToInt32(hist_h - marginBtm - MaskedHist.At<float>(i)));
                        Cv2.Rectangle(histImage, prePtR, postPtR, bkClr, -1, LineTypes.Link8, 0);
                    }
                }
                // 피크 찾기
                bool bPlus = false, bPlusBack = true;
                for (int i = 1; i < histSize; i++)
                {
                    float iDiff = MaskedHist.At<float>(i) - MaskedHist.At<float>(i - 1);
                    if (iDiff > 0)
                    {
                        bPlus = true;
                    }
                    else
                    {
                        bPlus = false;
                    }

                    if (bPlusBack && !bPlus)
                    {
                        mapPeakInfo[MaskedHist.At<float>(i - 1)] = i - 1;
                    }

                    bPlusBack = bPlus;
                }

                mapPeakInfo[MaskedHist.At<float>(255)] = 255;
                mapPeakInfo[MaskedHist.At<float>(0)] = 0;

                // 첫 번째, 두 번째 피크값 추출
                var keys = new List<float>(mapPeakInfo.Keys);
                if (keys.Count >= 2)
                {
                    float filstKey = keys[keys.Count - 1];
                    float secondKey = keys[keys.Count - 2];
                    iFisrtSecondPeak[0] = mapPeakInfo[filstKey];
                    iFisrtSecondPeak[1] = mapPeakInfo[secondKey];
                }
                iMeanPeak = (iFisrtSecondPeak[0] + iFisrtSecondPeak[1]) / 2;
            }

            return true;
        }
        public static bool OriginMake(Mat Byimg)
        {
            Mat matroiimg_gray_roi;
            int iRisingCnt = 0;
            int RisingPos = 0, FallingPos = 0;
            byte ucPixel = 0, ucPixel_back = 0;
            byte PixelData = 0;

            int BorderLength = (int)(Byimg.Rows / 100.0 + 0.5);
            int linePos = -999;
            double edgePer = 0.8;

            bool width = false, height = false;

            // 상단
            for (int i = 0; i < Byimg.Rows / 10; i++)
            {
                matroiimg_gray_roi = new Mat(Byimg, new Rect(0, i, Byimg.Cols, 1));
                iRisingCnt = 0;

                for (int j = 0; j < Byimg.Cols; j++)
                {
                    ucPixel = matroiimg_gray_roi.At<byte>(0, j);

                    if (i == 0 && j == 0)
                        ucPixel_back = ucPixel;

                    if (ucPixel != ucPixel_back)
                    {
                        if (ucPixel > ucPixel_back)
                        {
                            iRisingCnt++;
                            RisingPos = j;
                        }
                        else
                            FallingPos = j;
                    }

                    ucPixel_back = ucPixel;

                    if (iRisingCnt > 2)
                        break;
                }

                if (iRisingCnt == 1 && Math.Abs(FallingPos - RisingPos) > Byimg.Cols * edgePer)
                {
                    if (linePos == -999)
                        linePos = i;
                    else
                    {
                        if (Math.Abs(linePos - i) < 1) break;
                        else linePos = i;
                    }

                    PixelData = (byte)(FallingPos > RisingPos ? 255 : 0);

                    Cv2.Line(Byimg,
                        new Point(BorderLength, i),
                        new Point(Byimg.Cols - 1 - BorderLength, i),
                        new Scalar(PixelData), 1);
                    width = true;
                }
            }

            // 하단
            linePos = -999;
            for (int i = Byimg.Rows - 2; i > Byimg.Rows * 9 / 10; i--)
            {
                iRisingCnt = 0;
                matroiimg_gray_roi = new Mat(Byimg, new Rect(0, i, Byimg.Cols, 1));

                for (int j = 0; j < Byimg.Cols; j++)
                {
                    ucPixel = matroiimg_gray_roi.At<byte>(0, j);

                    if (i == 0 && j == 0)
                        ucPixel_back = ucPixel;

                    if (ucPixel != ucPixel_back)
                    {
                        if (ucPixel > ucPixel_back)
                        {
                            iRisingCnt++;
                            RisingPos = j;
                        }
                        else
                            FallingPos = j;
                    }

                    ucPixel_back = ucPixel;

                    if (iRisingCnt > 2)
                        break;
                }

                if (iRisingCnt == 1 && Math.Abs(FallingPos - RisingPos) > Byimg.Cols * edgePer)
                {
                    if (linePos == -999)
                        linePos = i;
                    else
                    {
                        if (Math.Abs(linePos - i) != 1) break;
                        else linePos = i;
                    }

                    PixelData = (byte)(FallingPos > RisingPos ? 255 : 0);

                    Cv2.Line(Byimg,
                        new Point(BorderLength, i),
                        new Point(Byimg.Cols - 1 - BorderLength, i),
                        new Scalar(PixelData), 1);
                    width = true;
                }
            }

            // 좌측
            linePos = -999;
            for (int i = 0; i < Byimg.Cols / 10; i++)
            {
                iRisingCnt = 0;
                matroiimg_gray_roi = new Mat(Byimg, new Rect(i, 0, 1, Byimg.Rows));

                for (int j = 0; j < Byimg.Rows; j++)
                {
                    ucPixel = matroiimg_gray_roi.At<byte>(j, 0);

                    if (i == 0 && j == 0)
                        ucPixel_back = ucPixel;

                    if (ucPixel != ucPixel_back)
                    {
                        if (ucPixel > ucPixel_back)
                        {
                            iRisingCnt++;
                            RisingPos = j;
                        }
                        else
                            FallingPos = j;
                    }

                    ucPixel_back = ucPixel;

                    if (iRisingCnt > 2)
                        break;
                }

                if (iRisingCnt == 1 && Math.Abs(FallingPos - RisingPos) > Byimg.Rows * edgePer)
                {
                    if (linePos == -999)
                        linePos = i;
                    else
                    {
                        if (Math.Abs(linePos - i) != 1) break;
                        else linePos = i;
                    }

                    PixelData = (byte)(FallingPos > RisingPos ? 255 : 0);

                    Cv2.Line(Byimg,
                        new Point(i, BorderLength),
                        new Point(i, Byimg.Rows - 1 - BorderLength),
                        new Scalar(PixelData), 1);
                    height = true;
                }
            }

            // 우측
            linePos = -999;
            for (int i = Byimg.Cols - 2; i > Byimg.Cols * 9 / 10; i--)
            {
                iRisingCnt = 0;
                matroiimg_gray_roi = new Mat(Byimg, new Rect(i, 0, 1, Byimg.Rows));

                for (int j = 0; j < Byimg.Rows; j++)
                {
                    ucPixel = matroiimg_gray_roi.At<byte>(j, 0);

                    if (i == 0 && j == 0)
                        ucPixel_back = ucPixel;

                    if (ucPixel != ucPixel_back)
                    {
                        if (ucPixel > ucPixel_back)
                        {
                            iRisingCnt++;
                            RisingPos = j;
                        }
                        else
                            FallingPos = j;
                    }

                    ucPixel_back = ucPixel;

                    if (iRisingCnt > 2)
                        break;
                }

                if (iRisingCnt == 1 && Math.Abs(FallingPos - RisingPos) > Byimg.Rows * edgePer)
                {
                    if (linePos == -999)
                        linePos = i;
                    else
                    {
                        if (Math.Abs(linePos - i) != 1) break;
                        else linePos = i;
                    }

                    PixelData = (byte)(FallingPos > RisingPos ? 255 : 0);

                    Cv2.Line(Byimg,
                        new Point(i, BorderLength),
                        new Point(i, Byimg.Rows - 1 - BorderLength),
                        new Scalar(PixelData), 1);
                    height = true;
                }
            }

            return height && width;
        }
        unsafe public static bool FindMCRCnt(Mat img, int iType, ref List<int>[] vecEdgePoints, ref int[] iIndex, ref int MatrixCnt, ref bool[] m_bMCROrigin, ref int m_iMCROrigin)
        {
            try
            {
                if (img.Empty()) return false;
                int Shape = 0;
                if (img.Cols * 1.5 < img.Rows)
                { Shape = 1; }
                else if (img.Rows * 1.5 < img.Cols)
                {
                    Shape = 2;
                }
                else
                {
                    Shape = 0;
                }

                Mat matTmp = new Mat();

                img.CopyTo(matTmp);
                if (matTmp.Channels() != 1)
                    Cv2.CvtColor(matTmp, matTmp, ColorConversionCodes.BGR2GRAY);
                if (iType == 1)
                {
                    matTmp = matTmp.T();
                    Cv2.Flip(matTmp, matTmp, FlipMode.Y);
                }
                else if (iType != 0)
                {
                    return false;
                }
                int iRowSize = (int)(matTmp.Rows / 12.0 + 0.5);// (int)(matTmp.Rows / Count);  
                MultiMap<int, int>[] mapEdgeSearch = new MultiMap<int, int>[2];
                MultiMap<int, int>[] mapRisingPos = new MultiMap<int, int>[2];
                MultiMap<int, int>[] mapFallingPos = new MultiMap<int, int>[2];
                HashSet<int>[] mapEdgeSearchKey = new HashSet<int>[2];

                for (int i = 0; i < 2; i++)
                {
                    mapEdgeSearch[i] = new MultiMap<int, int>();
                    mapRisingPos[i] = new MultiMap<int, int>();
                    mapFallingPos[i] = new MultiMap<int, int>();
                    mapEdgeSearchKey[i] = new HashSet<int>();
                }

                int[] MaxKey = new int[2];
                int[] repeatTar = new int[2];
                int mcr_version = 0;

                for (int updown = 0; updown < 2; updown++)
                {
                    if (updown == 1)
                        Cv2.Flip(matTmp, matTmp, FlipMode.XY);
                    int MaxKeyCnt = 0;
                    int iFindCutPos = -1;

                    for (int i = 0; i < iRowSize; i++)
                    {
                        int iRisingCnt = 0;
                        int iFallingCnt = 0;

                        byte* ptr = (byte*)matTmp.Ptr(i).ToPointer();
                        byte prev = ptr[0];

                        for (int j = 1; j < matTmp.Cols; j++)
                        {
                            byte cur = ptr[j];

                            if (cur != prev)
                            {
                                if (cur > prev)
                                {
                                    iRisingCnt++;
                                    mapRisingPos[updown].Add(i, j);
                                }
                                else
                                {
                                    iFallingCnt++;
                                    mapFallingPos[updown].Add(i, j);
                                }
                            }
                            prev = cur;
                        }

                        if (iRisingCnt != 0 && (iRisingCnt == 1 || iRisingCnt >= 5))
                        {
                            mapEdgeSearchKey[updown].Add(iRisingCnt);
                            mapEdgeSearch[updown].Add(iRisingCnt, i);
                        }
                    }

                    foreach (var key in mapEdgeSearchKey[updown])
                    {
                        int cnt = mapEdgeSearch[updown].Count(key);
                        if (cnt > MaxKeyCnt)
                        {
                            MaxKeyCnt = cnt;
                            MaxKey[updown] = key;
                        }
                    }

                    repeatTar[updown] =
                        (int)(MaxKeyCnt * MaxKeyCnt / (double)iRowSize + 0.5);

                    if (repeatTar[updown] < 1)
                        repeatTar[updown] = 1;

                    int sqrt = (int)Math.Sqrt(iRowSize);
                    if (repeatTar[updown] > sqrt)
                        repeatTar[updown] = sqrt;

                    var list = mapEdgeSearch[updown].GetValues(MaxKey[updown]);
                    if (list == null) return false;

                    int prevVal = -9999;
                    int repeatcnt = 0;

                    foreach (var v in list)
                    {
                        if (Math.Abs(v - prevVal) == 1)
                            repeatcnt++;
                        else
                            repeatcnt = 0;

                        if (repeatcnt >= repeatTar[updown])
                        {
                            iFindCutPos = v - repeatcnt;
                            break;
                        }

                        prevVal = v;
                    }

                    if (iFindCutPos == -1)
                        return false;

                    iIndex[updown] = iFindCutPos;

                    if (mcr_version < MaxKey[updown])
                        mcr_version = MaxKey[updown];
                }
                if (MatrixCnt == 0)
                    MatrixCnt = mcr_version * 2;
                if (MatrixCnt != 0 && Shape == 1)
                    MatrixCnt = mcr_version * 2;
                if (MatrixCnt == 0)
                    return false;

                vecEdgePoints[iType].Clear();

                bool bDotPos = MaxKey[0] < MaxKey[1];
                int idx = bDotPos ? 1 : 0;

                int repeat = repeatTar[idx];

                int[][] pts = new int[repeat][];
                int[] cnts = new int[repeat];

                for (int i = 0; i < repeat; i++)
                    pts[i] = new int[MatrixCnt + 4];

                for (int i = 0; i < repeat; i++)
                {
                    int cnt = 0;
                    int row = iIndex[idx] + i;

                    var rising = mapRisingPos[idx].GetValues(row);
                    if (rising != null)
                    {
                        foreach (var v in rising)
                        {
                            if (cnt > mcr_version + 2) break;
                            pts[i][cnt++] = v;
                        }
                    }

                    var falling = mapFallingPos[idx].GetValues(row);
                    if (falling != null)
                    {
                        foreach (var v in falling)
                        {
                            if (cnt > (MatrixCnt - 1) + 4) break;
                            pts[i][cnt++] = v;
                        }
                    }

                    Array.Sort(pts[i], 0, cnt);
                    cnts[i] = cnt;
                }

                for (int j = 0; j < MatrixCnt + 4; j++)
                {
                    long sum = 0;
                    int cnt = 0;

                    for (int i = 0; i < repeat; i++)
                    {
                        if (cnts[i] <= j) break;
                        sum += pts[i][j];
                        cnt++;
                    }

                    if (cnt == 0) continue;

                    int avg = (int)(sum / (double)cnt + 0.5);
                    if (avg == 0) continue;

                    if (iType == 1)
                        vecEdgePoints[iType].Add(bDotPos ? avg : matTmp.Cols - avg);
                    else
                        vecEdgePoints[iType].Add(bDotPos ? matTmp.Cols - avg : avg);
                }

                vecEdgePoints[iType].Sort();

                m_bMCROrigin[iType] = (MaxKey[0] != 1);

                if (iType == 1)
                {
                    if (m_bMCROrigin[0])
                        m_iMCROrigin = m_bMCROrigin[1] ? 4 : 3;
                    else
                        m_iMCROrigin = m_bMCROrigin[1] ? 2 : 1;
                }

                return true;
            }
            catch
            { return false; }
        }
    
        public static string RecognitionMatrix(Mat roiMat)
        {
            try
            {
                List<string> lstResult = new List<string>();
                string findResult = "";

                //Cv2.ImShow("d", roiMat);
                //Cv2.WaitKey(0);

                var result = zxing_reader.Decode(roiMat.ToBitmap());
                if (result != null) lstResult.Add(result.Text);

                //flip
                Mat flipMat = new Mat();
                if (lstResult.Count == 0)
                {
                    for (int i = -1; i < 2; i++)
                    {
                        Cv2.Flip(roiMat, flipMat, (FlipMode)i);
                        ZXing.IBarcodeReader zxing_reader = new BarcodeReader();
                        result = zxing_reader.Decode(flipMat.ToBitmap());
                        if (result != null)
                        {
                            lstResult.Add(result.Text);
                            break;
                        }
                    }
                }

                var group = lstResult.GroupBy(i => i);
                int maxCount = 0;
                foreach (var g in group)
                {
                    if (maxCount <= g.Count())
                    {
                        maxCount = g.Count();
                        findResult = g.Key;
                    }
                }

                return findResult;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }
    }
}

