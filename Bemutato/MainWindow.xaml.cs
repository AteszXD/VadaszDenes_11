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
using Bemutato.Assetts;

namespace VadaszDenes
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string[,] terkep = new string[50, 50];
        Button[,] jatekter = new Button[50, 50];

        private int roverX;
        private int roverY;

        private Rover rover; // rover logic (battery, time, stats)

        private FrameworkElement target; // Ezt a gombot/képet követjük
        private double smoothness = 0.1; // 0.0 és 1.0 között (kisebb = simább/lassabb)

        // New: current rover image filename (can be changed when moving left)
        private string roverImageName = "rover.png";

        public MainWindow()
        {
            BetoltTerkep();

            // Initialize Rover logic and sync starting position from the loaded map
            rover = new Rover();
            rover.Reset(roverX, roverY);

            InitializeComponent();
            JatekterGeneralas();
            JatekterMegjelenites();

            this.KeyDown += MainWindow_KeyDown;
            CompositionTarget.Rendering += UpdateCamera;

            // Initial UI sync
            UpdateStatusUI();
            lblStatus.Text = "Ready.";
        }

        private void JatekterMegjelenites()
        {
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
                            // Use the current roverImageName so the initial display matches facing state
                            overlayPath = System.IO.Path.Combine(baseDir, "Assetts", roverImageName);
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

                    this.Focus(); // A Window-ra irányítjuk a fókuszt
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
                    button.Focusable = false; // Ne tudják a gombok ellopni a fókuszt a Window elől
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
                string[] adatok = sorok[i].Split(',');
                for (int j = 0; j < terkep.GetLength(1); j++)
                {
                    terkep[i, j] = adatok[j];

                    // Itt keressük meg a rovert (S = Start/Source)
                    if (terkep[i, j] == "S")
                    {
                        roverX = i; // Eltároljuk a sor indexét
                        roverY = j; // Eltároljuk az oszlop indexét
                    }
                }
            }
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

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.W: case Key.Up: RoverMozgatas(-1, 0); break;
                case Key.S: case Key.Down: RoverMozgatas(1, 0); break;
                case Key.A: case Key.Left: RoverMozgatas(0, -1); break;
                case Key.D: case Key.Right: RoverMozgatas(0, 1); break;
                case Key.M:
                case Key.Space:
                    MineAtCurrentPosition();
                    break;
            }
        }

        private void RoverMozgatas(int eltolasX, int eltolasY)
        {
            // Figyelem: A tömbnél az első index [sor], a második a [oszlop]
            // A kódodban az 'X' általában a sort (függőleges), az 'Y' az oszlopot (vízszintes) jelenti
            int ujX = roverX + eltolasX;
            int ujY = roverY + eltolasY;

            if (ujX >= 0 && ujX < terkep.GetLength(0) && ujY >= 0 && ujY < terkep.GetLength(1))
            {
                if (terkep[ujX, ujY] == ".")
                {
                    // Decide facing sprite change only for horizontal movement:
                    if (eltolasY == -1)
                    {
                        roverImageName = "rover_left.png";
                    }
                    else if (eltolasY == 1)
                    {
                        roverImageName = "rover.png";
                    }

                    // Ask the rover logic to perform movement. Use Slow to preserve one-block-per-key press.
                    int stepsMoved;
                    string message;
                    bool moved = rover.TryMove(eltolasX, eltolasY, Rover.Speed.Slow, out stepsMoved, out message);

                    if (!moved)
                    {
                        // Show status/feedback (quick debug) — you can replace with a proper UI element later
                        this.Title = message;
                        lblStatus.Text = message;
                        UpdateStatusUI();
                        return;
                    }

                    // At this point rover.X and rover.Y have been updated by the Rover class.
                    // Update the map: clear old, set new.
                    terkep[roverX, roverY] = ".";
                    FrissitsEgyCellat(roverX, roverY);

                    // Save previous coords then update roverX/roverY to match Rover
                    roverX = rover.X;
                    roverY = rover.Y;

                    terkep[roverX, roverY] = "S";
                    FrissitsEgyCellat(roverX, roverY);

                    // Update camera target
                    target = jatekter[roverX, roverY];

                    // Update title with rover status for feedback (battery/time/stats)
                    this.Title = rover.GetStatus();
                    UpdateStatusUI();
                }
            }
        }

        // Ez a függvény CSAK EGY gomb tartalmát cseréli le, nem az egész pályát
        private void FrissitsEgyCellat(int sor, int oszlop)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var layer = new Grid();

            // Alapkőzet (Mars)
            string marsPath = System.IO.Path.Combine(baseDir, "Assetts", "mars.png");
            layer.Children.Add(new Image
            {
                Source = new BitmapImage(new Uri(marsPath)),
                Stretch = Stretch.Fill
            });

            // Mi van ott most? - Hagyományos switch blokk használata
            string symbol = terkep[sor, oszlop];
            string overlayName = null;

            switch (symbol)
            {
                case "#": overlayName = "akadaly.png"; break;
                case "B": overlayName = "kek_asvany.png"; break;
                case "Y": overlayName = "sarga_asvany.png"; break;
                case "G": overlayName = "zold_asvany.png"; break;
                case "S":
                    // Use the dynamic roverImageName so facing is reflected
                    overlayName = roverImageName;
                    break;
                default: overlayName = null; break;
            }

            if (overlayName != null)
            {
                string path = System.IO.Path.Combine(baseDir, "Assetts", overlayName);
                layer.Children.Add(new Image
                {
                    Source = new BitmapImage(new Uri(path)),
                    Stretch = Stretch.Fill,
                    IsHitTestVisible = false
                });
            }

            // A gomb tartalmának frissítése
            jatekter[sor, oszlop].Content = layer;
        }

        // UI helper: update right-side status panel
        private void UpdateStatusUI()
        {
            if (lblBattery != null) lblBattery.Text = $"Battery: {rover.Battery}/100";
            if (lblTime != null) lblTime.Text = $"Time: HalfHour={rover.HalfHourTick} ({(rover.IsDay ? "Day" : "Night")})";
            if (lblPosition != null) lblPosition.Text = $"Pos: {rover.X},{rover.Y}";
            if (lblSteps != null) lblSteps.Text = $"Steps moved: {rover.StepsMoved}";
            if (lblMinerals != null) lblMinerals.Text = $"Minerals: {rover.MineralsMined}";
        }

        // Attempt to mine at the rover's current map cell.
        private void MineAtCurrentPosition()
        {
            // Only allow mining if there's a mineral symbol under rover: B, Y, G
            string symbol = terkep[roverX, roverY];
            if (symbol != "B" && symbol != "Y" && symbol != "G")
            {
                lblStatus.Text = "No mineral here to mine.";
                return;
            }

            string message;
            bool success = rover.TryMine(out message);
            lblStatus.Text = message;

            if (success)
            {
                // Remove mineral from map and refresh the cell
                terkep[roverX, roverY] = "S"; // rover stands on S
                FrissitsEgyCellat(roverX, roverY);

                UpdateStatusUI();
            }
            else
            {
                // failed due to battery/time etc. still update UI
                UpdateStatusUI();
            }
        }

        private void BtnMine_Click(object sender, RoutedEventArgs e)
        {
            MineAtCurrentPosition();
        }
    }
}
