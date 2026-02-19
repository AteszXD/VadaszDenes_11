using System;
using System.Collections.Generic;
using System.IO;
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

namespace Bemutato
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string[,] terkep = new string[50, 50];
        Button[,] jatekter = new Button[50, 50];

        private FrameworkElement target; // Ezt a gombot/képet követjük
        private double smoothness = 0.1; // 0.0 és 1.0 között (kisebb = simább/lassabb)

        public MainWindow()
        {
            BetoltTerkep();
            InitializeComponent();
            JatekterGeneralas();
            JatekterMegjelenites();
        }

        private void JatekterMegjelenites()
        {
            for (int i = 0; i < terkep.GetLength(0); i++)
            {
                for (int j = 0; j < terkep.GetLength(1); j++)
                {
                    grdJatekter.Children.Add(jatekter[i, j]);
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string filePath;
                    switch (jatekter[i,j].Content)
                    {
                        case ".":
                            filePath = System.IO.Path.Combine(baseDir, "Assetts", "mars.png");
                            jatekter[i, j].Content = new Image()
                            {
                                Source = new BitmapImage(new Uri(filePath)),
                                Stretch = Stretch.Fill
                            };
                            break;
                        case "#":
                            filePath = System.IO.Path.Combine(baseDir, "Assetts", "akadaly.png");
                            jatekter[i, j].Content = new Image()
                            {
                                Source = new BitmapImage(new Uri(filePath)),
                                Stretch = Stretch.Fill
                            };
                            break;
                        case "B":
                            filePath = System.IO.Path.Combine(baseDir, "Assetts", "kek_asvany.png");
                            jatekter[i, j].Content = new Image()
                            {
                                Source = new BitmapImage(new Uri(filePath)),
                                Stretch = Stretch.Fill
                            };
                            break;
                        case "Y":
                            filePath = System.IO.Path.Combine(baseDir, "Assetts", "sarga_asvany.png");
                            jatekter[i, j].Content = new Image()
                            {
                                Source = new BitmapImage(new Uri(filePath)),
                                Stretch = Stretch.Fill
                            };
                            break;
                        case "G":
                            filePath = System.IO.Path.Combine(baseDir, "Assetts", "zold_asvany.png");
                            jatekter[i, j].Content = new Image()
                            {
                                Source = new BitmapImage(new Uri(filePath)),
                                Stretch = Stretch.Fill
                            };
                            break;
                        case "S":
                            filePath = System.IO.Path.Combine(baseDir, "Assetts", "rover.png");
                            jatekter[i, j].Content = new Image()
                            {
                                Source = new BitmapImage(new Uri(filePath)),
                                Stretch = Stretch.Fill
                            };
                            target = jatekter[i, j];
                            break;
                    }
                }
            }
        }

        private void JatekterGeneralas()
        {
            for (int i = 0; i < terkep.GetLength(0); i++)
            {
                for (int j = 0; j < terkep.GetLength(1); j++)
                {
                    Button button = new Button()
                    {
                        Content = terkep[i, j],
                        Height = 200,
                        Width = 200,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(10 + (j * 200), 10 + (i * 200), 0, 0),
                    };
                    button.Background = Brushes.Black;
                    button.Foreground = Brushes.White;
                    jatekter[i, j] = button;
                }
            }
        }

        private void BetoltTerkep()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string filePath = System.IO.Path.Combine(baseDir, "Assetts", "mars_map_50x50.csv");

            string[] sorok = File.ReadAllLines(filePath);
            for (int i = 0; i < terkep.GetLength(0); i++)
            {
                for (int j = 0; j < terkep.GetLength(1); j++)
                {
                    terkep[i, j] = sorok[i].Split(',')[j];
                }
            }

            //for (int i = 0; i < terkep.GetLength(0); i++)
            //{
            //    for (int j = 0; j < terkep.GetLength(1); j++)
            //    {
            //        terkep[i, j] = "A";
            //    }
            //}
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Csak akkor zoomolunk, ha a CTRL billentyű le van nyomva
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                double zoom = e.Delta > 0 ? 0.1 : -0.1;

                // Kiszámoljuk az új méretet
                double nextScale = st.ScaleX + zoom;

                // Korlátok: ne lehessen 20%-nál kisebb és 10x-esnél nagyobb
                if (nextScale >= 0.2 && nextScale <= 10.0)
                {
                    st.ScaleX = nextScale;
                    st.ScaleY = nextScale;
                }

                // Elnyomjuk az eseményt, hogy ne görgessen le-fel zoomolás közben
                e.Handled = true;
            }
        }

        private void UpdateCamera(object sender, EventArgs e)
        {
            if (target == null) return;

            // 1. Célpont pozíciója a Griden (zoommal korrigálva)
            Point targetPos = target.TransformToAncestor(grdJatekter).Transform(new Point(0, 0));
            double destX = (targetPos.X + target.ActualWidth / 2) * st.ScaleX - (ScrollViewer.ActualWidth / 2);
            double destY = (targetPos.Y + target.ActualHeight / 2) * st.ScaleY - (ScrollViewer.ActualHeight / 2);

            // 2. Jelenlegi pozíció
            double currentX = ScrollViewer.HorizontalOffset;
            double currentY = ScrollViewer.VerticalOffset;

            // 3. Sima átmenet (Lerp formula: current + (target - current) * smoothness)
            double nextX = currentX + (destX - currentX) * smoothness;
            double nextY = currentY + (destY - currentY) * smoothness;

            // 4. Mozgatás
            ScrollViewer.ScrollToHorizontalOffset(nextX);
            ScrollViewer.ScrollToVerticalOffset(nextY);
        }
    }
}
