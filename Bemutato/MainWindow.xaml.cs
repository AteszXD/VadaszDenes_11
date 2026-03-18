using Bemutato.Assetts;
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
    /// Interakciós logika a MainWindow.xaml-hez
    /// </summary>
    public partial class MainWindow : Window
    {

        string[,] terkep = new string[50, 50];
        Button[,] jatekGombok = new Button[50, 50];

        private int roverX;
        private int roverY;
        private SimplePathfinder utkereso;
        private Rover rover; // rover logika (akkumulátor, idő, statisztikák)

        private FrameworkElement kovetettCel; // Ezt a gombot/képet követjük
        private double simasag = 0.1; // 0.0 és 1.0 között (kisebb = simább/lassabb)

        // Új: aktuális rover kép fájlnév (változhat balra mozgáskor)
        private string roverKepNev = "rover.png";

        public MainWindow()
        {
            TerkepBetoltese();

            // Rover logika inicializálása és kezdőpozíció szinkronizálása a betöltött térképről
            rover = new Rover();
            rover.Reset(roverX, roverY);

            InitializeComponent();
            JatekTerGeneralas();
            JatekTerMegjelenites();
            utkereso = new SimplePathfinder(terkep);

            this.KeyDown += MainWindow_KeyDown;
            CompositionTarget.Rendering += KameraFrissites;

            // Kezdeti UI szinkronizálás
            AllapotFrissites();
            // lblStatus.Text = "Kész.";
        }

        private void JatekTerMegjelenites()
        {
            string baseMappa = AppDomain.CurrentDomain.BaseDirectory;

            for (int i = 0; i < terkep.GetLength(0); i++)
            {
                for (int j = 0; j < terkep.GetLength(1); j++)
                {
                    // Gomb hozzáadása a szülő gridhez
                    grdJatekter.Children.Add(jatekGombok[i, j]);

                    // Tároló létrehozása a rétegekhez (alap + opcionális fedőréteg)
                    var retegek = new Grid();

                    // Alapkép: mindig mars.png
                    string marsUtvonal = System.IO.Path.Combine(baseMappa, "Assetts", "mars.png");
                    var alapKep = new Image()
                    {
                        Source = new BitmapImage(new Uri(marsUtvonal)),
                        Stretch = Stretch.Fill
                    };
                    retegek.Children.Add(alapKep);

                    // Fedőréteg meghatározása az eredeti tartalom alapján (térkép szimbólum)
                    string szimbolum = terkep[i, j]; // térkép adat használata a korábbi Content helyett
                    string fedoUtvonal = null;

                    switch (szimbolum)
                    {
                        case "#":
                            fedoUtvonal = System.IO.Path.Combine(baseMappa, "Assetts", "akadaly.png");
                            break;
                        case "B":
                            fedoUtvonal = System.IO.Path.Combine(baseMappa, "Assetts", "kek_asvany.png");
                            break;
                        case "Y":
                            fedoUtvonal = System.IO.Path.Combine(baseMappa, "Assetts", "sarga_asvany.png");
                            break;
                        case "G":
                            fedoUtvonal = System.IO.Path.Combine(baseMappa, "Assetts", "zold_asvany.png");
                            break;
                        case "S":
                            // Az aktuális roverKepNev használata, hogy a kezdeti megjelenítés tükrözze a rover irányát
                            fedoUtvonal = System.IO.Path.Combine(baseMappa, "Assetts", roverKepNev);
                            break;
                            // case "." -> nincs fedőréteg (csak mars látható)
                    }

                    if (!string.IsNullOrEmpty(fedoUtvonal))
                    {
                        var fedoKep = new Image()
                        {
                            Source = new BitmapImage(new Uri(fedoUtvonal)),
                            Stretch = Stretch.Fill
                        };
                        // Biztosítjuk, hogy a fedőrétegek ne blokkolják a gomb kattintási eseményeket, ha szükséges
                        fedoKep.IsHitTestVisible = false;
                        retegek.Children.Add(fedoKep);

                        if (szimbolum == "S")
                        {
                            // Célként a gombot tartjuk meg, hogy a kamera logika továbbra is megtalálja a gomb pozícióját
                            kovetettCel = jatekGombok[i, j];
                        }
                    }

                    // Rétegezett Grid beállítása a gomb tartalmaként
                    jatekGombok[i, j].Content = retegek;

                    this.Focus(); // Fókusz a Window-ra irányítása
                }
            }
        }

        private void JatekTerGeneralas()
        {
            for (int i = 0; i < terkep.GetLength(0); i++)
            {
                for (int j = 0; j < terkep.GetLength(1); j++)
                {
                    Button gomb = new Button()
                    {
                        Content = terkep[i, j],
                        Height = 200,
                        Width = 200,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(10 + (j * 200), 10 + (i * 200), 0, 0),
                    };
                    gomb.Background = Brushes.Black;
                    gomb.Foreground = Brushes.White;
                    gomb.Focusable = false; // Ne tudják a gombok ellopni a fókuszt a Window elől
                    jatekGombok[i, j] = gomb;
                }
            }
        }

        private void TerkepBetoltese()
        {
            string baseMappa = AppDomain.CurrentDomain.BaseDirectory;
            string fajlUtvonal = System.IO.Path.Combine(baseMappa, "Assetts", "mars_map_50x50.csv");

            string[] sorok = File.ReadAllLines(fajlUtvonal);
            for (int i = 0; i < terkep.GetLength(0); i++)
            {
                string[] adatok = sorok[i].Split(',');
                for (int j = 0; j < terkep.GetLength(1); j++)
                {
                    terkep[i, j] = adatok[j];

                    // Itt keressük meg a rovert (S = Start/Forrás)
                    if (terkep[i, j] == "S")
                    {
                        roverX = i; // Sor index tárolása
                        roverY = j; // Oszlop index tárolása
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

                // Új méret kiszámítása
                double kovetkezoMeret = st.ScaleX + zoom;

                // Korlátok: ne lehessen 20%-nál kisebb és 10x-esnél nagyobb
                if (kovetkezoMeret >= 0.2 && kovetkezoMeret <= 10.0)
                {
                    st.ScaleX = kovetkezoMeret;
                    st.ScaleY = kovetkezoMeret;
                }

                // Esemény elnyomása, hogy ne görgessen le-fel zoomolás közben
                e.Handled = true;
            }
        }

        private void KameraFrissites(object sender, EventArgs e)
        {
            if (kovetettCel == null) return;

            // ScrollViewer példány megkeresése a XAML-ben (különböző elnevezések kezelése)
            var sv = this.FindName("ScrollViewer") as ScrollViewer ?? this.FindName("scrollViewer") as ScrollViewer;
            if (sv == null) return; // nem lehet folytatni ScrollViewer példány nélkül
            if (st == null) return; // ScaleTransform létezésének ellenőrzése

            // 1. Cél pozíciója a grid-en
            Point celPozicio = kovetettCel.TransformToAncestor(grdJatekter).Transform(new Point(0, 0));

            // Tényleges méretek használata a példányból (statikus elérésű kétértelműség elkerülése)
            double celKozepX = celPozicio.X + kovetettCel.RenderSize.Width / 2.0;
            double celKozepY = celPozicio.Y + kovetettCel.RenderSize.Height / 2.0;

            // Aktuális skála alkalmazása és nézet középre igazítása a célon
            double celX = celKozepX * st.ScaleX - (sv.ActualWidth / 2.0);
            double celY = celKozepY * st.ScaleY - (sv.ActualHeight / 2.0);

            // 2. Aktuális pozíció a ScrollViewer-ből
            double aktualisX = sv.HorizontalOffset;
            double aktualisY = sv.VerticalOffset;

            // 3. Sima interpoláció
            double kovetkezoX = aktualisX + (celX - aktualisX) * simasag;
            double kovetkezoY = aktualisY + (celY - aktualisY) * simasag;

            // 4. ScrollViewer mozgatása
            sv.ScrollToHorizontalOffset(kovetkezoX);
            sv.ScrollToVerticalOffset(kovetkezoY);
        }

        private async void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.W: case Key.Up: RoverMozgatasa(-1, 0); break;
                case Key.S: case Key.Down: RoverMozgatasa(1, 0); break;
                case Key.A: case Key.Left: RoverMozgatasa(0, -1); break;
                case Key.D: case Key.Right: RoverMozgatasa(0, 1); break;
                case Key.Space:
                    MessageBox.Show("SPACE lenyomva");
                    await OsszesAsvanyGyujteseAsync();
                    break;
            }
        }

        private void RoverMozgatasa(int eltolasX, int eltolasY)
        {
            int ujX = roverX + eltolasX;
            int ujY = roverY + eltolasY;

            if (ujX >= 0 && ujX < terkep.GetLength(0) && ujY >= 0 && ujY < terkep.GetLength(1))
            {
                string cella = terkep[ujX, ujY];

                // Ha ásvány van ott, próbáljuk meg felvenni
                if (cella == "B" || cella == "Y" || cella == "G")
                {
                    if (rover.TryBanyasz(out string uzenet))
                    {
                        // Térképről eltüntetjük az ásványt
                        terkep[ujX, ujY] = ".";
                        CellaFrissitese(ujX, ujY);

                        // Eseménynapló frissítése
                        NaploLista.Items.Add($"Ásvány begyűjtve ({ujX},{ujY}): {uzenet}");
                        NaploLista.ScrollIntoView(NaploLista.Items[NaploLista.Items.Count - 1]);

                        // Ásványszámláló frissítése
                        AsvanySzoveg.Text = rover.AsvanyokSzama.ToString();
                    }
                    else
                    {
                        // Ha nem sikerült bányászni (pl. kevés akkumulátor)
                        NaploLista.Items.Add($"Nem sikerült bányászni ({ujX},{ujY}): {uzenet}");
                        NaploLista.ScrollIntoView(NaploLista.Items[NaploLista.Items.Count - 1]);
                    }
                }

                // Ha átjárható a mező, mozgatjuk a rovert
                if (cella == ".")
                {
                    // Régi pozíció frissítése
                    terkep[roverX, roverY] = ".";
                    CellaFrissitese(roverX, roverY);

                    // Koordináták frissítése
                    roverX = ujX;
                    roverY = ujY;

                    // Rover pozíció frissítése a térképen
                    terkep[roverX, roverY] = "S";
                    CellaFrissitese(roverX, roverY);

                    // Kamera célpontjának frissítése
                    kovetettCel = jatekGombok[roverX, roverY];
                }

                // Alsó státuszpanel frissítése
                AllapotFrissites();
            }
        }

        // Ez a függvény CSAK EGY gomb tartalmát cseréli le, nem az egész pályát
        private void CellaFrissitese(int sor, int oszlop)
        {
            string baseMappa = AppDomain.CurrentDomain.BaseDirectory;
            var retegek = new Grid();

            // Alapkőzet (Mars)
            string marsUtvonal = System.IO.Path.Combine(baseMappa, "Assetts", "mars.png");
            retegek.Children.Add(new Image
            {
                Source = new BitmapImage(new Uri(marsUtvonal)),
                Stretch = Stretch.Fill
            });

            // Mi van ott most? - Hagyományos switch blokk használata
            string szimbolum = terkep[sor, oszlop];
            string fedoNev = null;

            switch (szimbolum)
            {
                case "#": fedoNev = "akadaly.png"; break;
                case "B": fedoNev = "kek_asvany.png"; break;
                case "Y": fedoNev = "sarga_asvany.png"; break;
                case "G": fedoNev = "zold_asvany.png"; break;
                case "S":
                    // Dinamikus roverKepNev használata, hogy az irány tükröződjön
                    fedoNev = roverKepNev;
                    break;
                default: fedoNev = null; break;
            }

            if (fedoNev != null)
            {
                string utvonal = System.IO.Path.Combine(baseMappa, "Assetts", fedoNev);
                retegek.Children.Add(new Image
                {
                    Source = new BitmapImage(new Uri(utvonal)),
                    Stretch = Stretch.Fill,
                    IsHitTestVisible = false
                });
            }

            // Gomb tartalmának frissítése
            jatekGombok[sor, oszlop].Content = retegek;
        }

        // UI segédfüggvény: státuszpanel frissítése
        private void AllapotFrissites()
        {
            if (lblAkkumulator != null) lblAkkumulator.Text = $"Akkumulátor: {rover.Akku}/100";

            // Rover.TimeOfDayString használata, ami 24 órás ciklusba csomagolja az időt.
            // Nap/éjszaka jelző és a napi cikluson belüli félóra index is.
            if (lblIdo != null)
            {
                string idoString = rover.NapszakString ?? $"{rover.FeloraTick / 2}:{(rover.FeloraTick % 2 * 30).ToString("D2")}";
                string ciklusInfo = string.Empty;
                lblIdo.Text = $"Idő: {idoString} ({(rover.IsNappal ? "Nappal" : "Éjszaka")})";
            }

            if (lblPozicio != null) lblPozicio.Text = $"Pozíció: {rover.X},{rover.Y}";
            if (lblLepesek != null) lblLepesek.Text = $"Lépések: {rover.LepesSzam}";
            if (lblAsvanyok != null) lblAsvanyok.Text = $"Kibányászott ásványok: {rover.AsvanyokSzama}";
        }

        // Bányászási kísérlet a rover jelenlegi pozícióján
        private void BanyaszasAktualisPozicion()
        {
            // Csak akkor engedélyezett a bányászás, ha van ásvány szimbólum a rover alatt: B, Y, G
            string szimbolum = terkep[roverX, roverY];
            if (szimbolum != "B" && szimbolum != "Y" && szimbolum != "G")
            {
                // lblStatus.Text = "Nincs itt ásvány.";
                return;
            }

            string uzenet;
            bool siker = rover.TryBanyasz(out uzenet);
            // lblStatus.Text = uzenet;

            if (siker)
            {
                // Ásvány eltávolítása a térképről és cella frissítése
                terkep[roverX, roverY] = "S"; // rover az S-en áll
                CellaFrissitese(roverX, roverY);

                AllapotFrissites();
            }
            else
            {
                // sikertelenség akkumulátor/idő miatt stb., UI frissítése
                AllapotFrissites();
            }
        }

        private void BtnBanyasz_Click(object sender, RoutedEventArgs e)
        {
            BanyaszasAktualisPozicion();
        }

        private async Task<bool> UtkeresesCelhozAsync(int celX, int celY)
        {
            var utvonal = UtvonalKeresese(roverX, roverY, celX, celY);
            if (utvonal == null)
            {
                NaploLista.Items.Add($"Nincs elérhető útvonal ({celX},{celY})!");
                NaploLista.ScrollIntoView(NaploLista.Items[NaploLista.Items.Count - 1]);
                return false;
            }

            // Első elem a jelenlegi pozíció, azt hagyjuk ki
            foreach (var lepes in utvonal.Skip(1))
            {
                int dx = lepes.X - roverX;
                int dy = lepes.Y - roverY;

                Rover.Sebesseg sebesseg = Rover.Sebesseg.Normal;
                int v = (int)sebesseg;
                int szuksegesEnergia = 2 * v * v;
                int akkuLepesUtan = rover.Akku - szuksegesEnergia + (rover.IsNappal ? 10 : 0);

                if (akkuLepesUtan < 0)
                {
                    rover.VarakozasEgyFelora(out string uzenetVar);
                    NaploLista.Items.Add(uzenetVar);
                    AllapotFrissites();
                    await Task.Delay(50);
                    continue;
                }

                RoverMozgatasa(dx, dy);
                AllapotFrissites();
                await Task.Delay(50);

                // Ásvány felszedése az aktuális mezőn
                string aktualisCella = terkep[roverX, roverY];
                if (aktualisCella == "B" || aktualisCella == "Y" || aktualisCella == "G")
                {
                    terkep[roverX, roverY] = ".";
                    CellaFrissitese(roverX, roverY);

                    if (rover.TryBanyasz(out string uzenetBanyasz))
                    {
                        NaploLista.Items.Add($"Felszedett ásvány ({roverX},{roverY}): {uzenetBanyasz}");
                        NaploLista.ScrollIntoView(NaploLista.Items[NaploLista.Items.Count - 1]);
                        AllapotFrissites();
                    }
                }
            }

            return true;
        }

        private async Task OsszesAsvanyGyujteseAsync()
        {
            // 1️⃣ Lista készítése az összes ásványról
            List<(int X, int Y)> asvanyok = new List<(int X, int Y)>();
            for (int i = 0; i < 50; i++)
            {
                for (int j = 0; j < 50; j++)
                {
                    if (terkep[i, j] == "B" || terkep[i, j] == "Y" || terkep[i, j] == "G")
                    {
                        asvanyok.Add((i, j));
                    }
                }
            }

            // 2️⃣ Amíg van ásvány a listában
            while (asvanyok.Count > 0)
            {
                // 2a️⃣ Legközelebbi ásvány kiválasztása (Manhattan távolság)
                var kovetkezoCel = asvanyok.OrderBy(m => Math.Abs(m.X - roverX) + Math.Abs(m.Y - roverY)).First();

                // 2b️⃣ Útvonal keresés a célhoz
                await UtkeresesCelhozAsync(kovetkezoCel.X, kovetkezoCel.Y);

                // 2c️⃣ Felszedett ásvány eltávolítása a listából
                asvanyok.RemoveAll(m => m.X == roverX && m.Y == roverY);

                // Rövid delay, hogy UI frissüljön
                await Task.Delay(50);
            }

            NaploLista.Items.Add("Minden elérhető ásványt felszedett a rover!");
            NaploLista.ScrollIntoView(NaploLista.Items[NaploLista.Items.Count - 1]);
        }

        private List<(int X, int Y)> UtvonalKeresese(int startX, int startY, int celX, int celY)
        {
            var nyitott = new List<Csomopont>();
            var zart = new HashSet<(int, int)>();

            nyitott.Add(new Csomopont(startX, startY));

            while (nyitott.Count > 0)
            {
                // Legkisebb F-értékű csomópont kiválasztása
                var aktualis = nyitott.OrderBy(n => n.F).First();
                nyitott.Remove(aktualis);
                zart.Add((aktualis.X, aktualis.Y));

                // Ha célhoz értünk, építsük vissza az utat
                if (aktualis.X == celX && aktualis.Y == celY)
                {
                    var utvonal = new List<(int, int)>();
                    var csomopont = aktualis;
                    while (csomopont != null)
                    {
                        utvonal.Add((csomopont.X, csomopont.Y));
                        csomopont = csomopont.Szulo;
                    }
                    utvonal.Reverse();
                    return utvonal;
                }

                // Szomszédok: 8 irány (diagonális is)
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        int nx = aktualis.X + dx;
                        int ny = aktualis.Y + dy;

                        // Határok ellenőrzése
                        if (nx < 0 || nx >= 50 || ny < 0 || ny >= 50) continue;

                        // Akadályok kizárása
                        if (terkep[nx, ny] == "#") continue;

                        if (zart.Contains((nx, ny))) continue;

                        int gKoltseg = aktualis.G + 1;
                        int hKoltseg = Math.Abs(celX - nx) + Math.Abs(celY - ny); // Manhattan távolság
                        var szomszed = new Csomopont(nx, ny, aktualis) { G = gKoltseg, H = hKoltseg };

                        // Ha van már jobb út a nyitott listában, ne adjuk hozzá
                        var letezo = nyitott.FirstOrDefault(n => n.X == nx && n.Y == ny);
                        if (letezo != null && letezo.G <= szomszed.G) continue;

                        nyitott.Add(szomszed);
                    }
                }
            }

            // Nincs útvonal
            return null;
        }
    }

    // Belső osztály az A* algoritmushoz
    public class Csomopont
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int G { get; set; } // Kezdőponttól számított költség
        public int H { get; set; } // Heurisztika (becsült távolság a célig)
        public int F => G + H; // Teljes becsült költség
        public Csomopont Szulo { get; set; }

        public Csomopont(int x, int y, Csomopont szulo = null)
        {
            X = x;
            Y = y;
            Szulo = szulo;
        }
    }
}