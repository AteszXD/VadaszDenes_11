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

namespace VadaszDenes
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
            // Plan (pseudocode):
            // 1. Precompute base directory
            // 2. For each cell:
            //    a. Add the button to the grid
            //    b. Create a Grid container for layered images
            //    c. Always create and add the base Image (mars.png)
            //    d. If the map symbol is not ".", create an overlay Image with the corresponding transparent PNG and add it on top
            //    e. If the symbol is "S", also mark the button as the camera target
            //    f. Set the button.Content to the composed Grid
            //
            // This ensures mars.png is always rendered and everything else is drawn over it (transparent PNG overlays).

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            for (int i = 0; i < terkep.GetLength(0); i++)
            {
                for (int j = 0; j < terkep.GetLength(1); j++)
                {
                    // Add the button to the parent grid
                    grdJatekter.Children.Add(jatekter[i, j]);

                    // Create a container to layer images (base + optional overlay)
                    var layer = new Grid();

                    // Base image: always mars.png
                    string baseMarsPath = System.IO.Path.Combine(baseDir, "Assetts", "mars.png");
                    var baseImage = new Image()
                    {
                        Source = new BitmapImage(new Uri(baseMarsPath)),
                        Stretch = Stretch.Fill
                    };
                    layer.Children.Add(baseImage);

                    // Determine overlay based on the original content (the map symbol)
                    string symbol = terkep[i, j]; // use the map data rather than previous Content
                    string overlayPath = null;

                    switch (symbol)
                    {
                        case "#":
                            overlayPath = System.IO.Path.Combine(baseDir, "Assetts", "akadaly.png");
                            break;
                        case "B":
                            overlayPath = System.IO.Path.Combine(baseDir, "Assetts", "kek_asvany.png");
                            break;
                        case "Y":
                            overlayPath = System.IO.Path.Combine(baseDir, "Assetts", "sarga_asvany.png");
                            break;
                        case "G":
                            overlayPath = System.IO.Path.Combine(baseDir, "Assetts", "zold_asvany.png");
                            break;
                        case "S":
                            overlayPath = System.IO.Path.Combine(baseDir, "Assetts", "rover.png");
                            break;
                            // case "." -> no overlay (only mars visible)
                    }

                    if (!string.IsNullOrEmpty(overlayPath))
                    {
                        var overlayImage = new Image()
                        {
                            Source = new BitmapImage(new Uri(overlayPath)),
                            Stretch = Stretch.Fill
                        };
                        // Make sure overlays don't block click events on the button if needed
                        overlayImage.IsHitTestVisible = false;
                        layer.Children.Add(overlayImage);

                        if (symbol == "S")
                        {
                            // Keep target as the Button so camera logic still finds the button position
                            target = jatekter[i, j];
                        }
                    }

                    // Assign the layered Grid as the button's content
                    jatekter[i, j].Content = layer;
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

            // Try to locate the ScrollViewer instance defined in XAML (handles different naming)
            var sv = this.FindName("ScrollViewer") as ScrollViewer ?? this.FindName("scrollViewer") as ScrollViewer;
            if (sv == null) return; // cannot proceed without the ScrollViewer instance
            if (st == null) return; // ensure the ScaleTransform exists

            // 1. Target position on the grid
            Point targetPos = target.TransformToAncestor(grdJatekter).Transform(new Point(0, 0));

            // Use RenderSize to get actual dimensions from the instance (avoids static-access ambiguity)
            double targetCenterX = targetPos.X + target.RenderSize.Width / 2.0;
            double targetCenterY = targetPos.Y + target.RenderSize.Height / 2.0;

            // Apply current scale and center the view on the target
            double destX = targetCenterX * st.ScaleX - (sv.ActualWidth / 2.0);
            double destY = targetCenterY * st.ScaleY - (sv.ActualHeight / 2.0);

            // 2. Current position from the ScrollViewer instance
            double currentX = sv.HorizontalOffset;
            double currentY = sv.VerticalOffset;

            // 3. Smooth interpolation
            double nextX = currentX + (destX - currentX) * smoothness;
            double nextY = currentY + (destY - currentY) * smoothness;

            // 4. Move the ScrollViewer (instance calls)
            sv.ScrollToHorizontalOffset(nextX);
            sv.ScrollToVerticalOffset(nextY);
        }
    }
}
