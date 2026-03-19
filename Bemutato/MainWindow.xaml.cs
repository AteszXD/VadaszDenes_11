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
        private double simasag = 0.05; // Még simább mozgás (0.05 = nagyon lassú követés)

        // Új: aktuális rover kép fájlnév (változhat balra mozgáskor)
        private string roverKepNev = "rover.png";

        // Új: sebesség ikonok a megjelenítéshez
        private Dictionary<Rover.Sebesseg, string> sebessegIkonok = new Dictionary<Rover.Sebesseg, string>
        {
            { Rover.Sebesseg.Lassu, "🐢" },
            { Rover.Sebesseg.Normal, "🚶" },
            { Rover.Sebesseg.Gyors, "⚡" }
        };

        // Animáció késleltetése (ms)
        private const int ANIMACIOS_KESLELTETES = 200;

        // Gyors sebesség számláló (korlátozáshoz)
        private int gyorsSebessegSzamlalo = 0;

        public MainWindow()
        {
            TerkepBetoltese();

            // Rover logika inicializálása az App-ból vett időkerettel
            App app = Application.Current as App;
            int eloirhetoIdoOrak = app?.ElerhetoIdokeretOrak ?? 24;
            
            rover = new Rover(eloirhetoIdoOrak);
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

            // 3. Sima interpoláció (még lassabb követés)
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
                case Key.W: case Key.Up: RoverMozgatasa(-1, 0, Rover.Sebesseg.Normal); break;
                case Key.S: case Key.Down: RoverMozgatasa(1, 0, Rover.Sebesseg.Normal); break;
                case Key.A: case Key.Left: RoverMozgatasa(0, -1, Rover.Sebesseg.Normal); break;
                case Key.D: case Key.Right: RoverMozgatasa(0, 1, Rover.Sebesseg.Normal); break;
                case Key.Space:
                    await OsszesAsvanyGyujteseAsync();
                    break;
                // Sebességváltás teszteléshez (opcionális)
                case Key.D1:
                    rover.AktualisSebesseg = Rover.Sebesseg.Lassu;
                    NaploLista.Items.Add($"{sebessegIkonok[Rover.Sebesseg.Lassu]} Sebesség: Lassú");
                    AllapotFrissites();
                    break;
                case Key.D2:
                    rover.AktualisSebesseg = Rover.Sebesseg.Normal;
                    NaploLista.Items.Add($"{sebessegIkonok[Rover.Sebesseg.Normal]} Sebesség: Normál");
                    AllapotFrissites();
                    break;
                case Key.D3:
                    rover.AktualisSebesseg = Rover.Sebesseg.Gyors;
                    NaploLista.Items.Add($"{sebessegIkonok[Rover.Sebesseg.Gyors]} Sebesség: Gyors");
                    AllapotFrissites();
                    break;
            }
        }

        /// <summary>
        /// Ellenőrzi, hogy egy adott pozíció a pályán belül van-e és járható-e
        /// </summary>
        private bool IsJarhato(int x, int y)
        {
            // Pálya határainak ellenőrzése
            if (x < 0 || x >= terkep.GetLength(0) || y < 0 || y >= terkep.GetLength(1))
                return false;

            // Akadály ellenőrzése
            string cella = terkep[x, y];
            return cella != "#";
        }

        /// <summary>
        /// Intelligens sebességválasztás az energiaszint és napszak alapján (biztonsági)
        /// </summary>
        private Rover.Sebesseg BiztonsagosSebessegValasztas(Rover.Sebesseg kertSebesseg)
        {
            // ÉJSZAKA: csak lassú sebesség engedélyezett!
            if (!rover.IsNappal)
            {
                if (rover.Akku >= 4) // Lassú mozgáshoz + tartalék
                {
                    return Rover.Sebesseg.Lassu;
                }
                else
                {
                    NaploLista.Items.Add($"🌙 Éjszaka nincs elég energia a mozgáshoz! (Akku: {rover.Akku})");
                    return Rover.Sebesseg.Lassu;
                }
            }

            // NAPPAL: dinamikus sebességválasztás

            // GYORS SEBESSÉG (3-as): csak ha bőven van energia és ritkán
            if (kertSebesseg == Rover.Sebesseg.Gyors)
            {
                gyorsSebessegSzamlalo++;

                // Feltételek a gyors sebességhez:
                // 1. Legalább 35 energia kell
                // 2. Csak minden 4. alkalommal
                // 3. A mozgás után is maradjon legalább 15

                int energiaFogyasztas = 2 * 3 * 3; // 18
                int toltes = 10;
                int energiaMozgasUtan = rover.Akku - energiaFogyasztas + toltes;

                bool lehetGyors = (rover.Akku >= 35) &&
                                  (gyorsSebessegSzamlalo % 4 == 0) &&
                                  (energiaMozgasUtan >= 15);

                if (lehetGyors)
                {
                    NaploLista.Items.Add($"⚡ GYORS SEBESSÉG engedélyezve! (Akku: {rover.Akku}, maradék: {energiaMozgasUtan})");
                    return Rover.Sebesseg.Gyors;
                }

                // Ha nem lehet gyors, próbáljuk normállal
                if (gyorsSebessegSzamlalo % 4 != 0)
                {
                    NaploLista.Items.Add($"⚡ Gyors sebesség csak minden 4. lépésben! (Most {gyorsSebessegSzamlalo}. lépés)");
                }
                else
                {
                    NaploLista.Items.Add($"⚡ Gyors sebességhez több energia kell! (Akku: {rover.Akku}, szükséges: 35+)");
                }

                kertSebesseg = Rover.Sebesseg.Normal;
            }

            // NORMÁL SEBESSÉG (2-es): ha van legalább 20 energia
            if (kertSebesseg == Rover.Sebesseg.Normal)
            {
                int energiaFogyasztas = 2 * 2 * 2; // 8
                int toltes = 10;
                int energiaMozgasUtan = rover.Akku - energiaFogyasztas + toltes;

                if (rover.Akku >= 20 && energiaMozgasUtan >= 12)
                {
                    return Rover.Sebesseg.Normal;
                }

                if (rover.Akku < 20)
                {
                    NaploLista.Items.Add($"🚶 Normál sebességhez több energia kell! (Akku: {rover.Akku}, szükséges: 20+)");
                }
            }

            // LASSÚ SEBESSÉG (1-es): alapértelmezett
            return Rover.Sebesseg.Lassu;
        }

        private async void RoverMozgatasa(int eltolasX, int eltolasY, Rover.Sebesseg sebesseg)
        {
            // Először ellenőrizzük, hogy egyáltalán van-e értelmes irány
            if (eltolasX == 0 && eltolasY == 0)
            {
                NaploLista.Items.Add($"⚠️ Érvénytelen mozgási irány");
                await Task.Delay(ANIMACIOS_KESLELTETES);
                return;
            }

            // ÉJSZAKAI KORLÁTOZÁS: csak lassú sebesség
            if (!rover.IsNappal && sebesseg != Rover.Sebesseg.Lassu)
            {
                NaploLista.Items.Add($"🌙 Éjszaka csak lassú sebesség engedélyezett! ({sebesseg} → Lassú)");
                sebesseg = Rover.Sebesseg.Lassu;
            }

            // Biztonsági sebességválasztás (soha ne fogyjon ki az aksi)
            Rover.Sebesseg tenylegesSebesseg = BiztonsagosSebessegValasztas(sebesseg);

            int lepesSzam = (int)tenylegesSebesseg;
            int aktualisX = roverX;
            int aktualisY = roverY;
            int megtettLepesek = 0;

            // Ha a biztonsági választás miatt lassítottunk, jelezzük
            if (tenylegesSebesseg != sebesseg)
            {
                NaploLista.Items.Add($"⚠️ Energiatakarékosság: {sebesseg} → {tenylegesSebesseg} (Akku: {rover.Akku})");
            }

            // Először számoljuk ki, hány lépést tudunk megtenni az energia alapján
            int maximalisLepes = lepesSzam;
            int energiaSzukseglet = 2 * maximalisLepes * maximalisLepes;
            int toltes = rover.IsNappal ? 10 : 0;

            // Csökkentsük a lépésszámot, ha kell
            while (maximalisLepes > 0 && rover.Akku - energiaSzukseglet + toltes < 2)
            {
                maximalisLepes--;
                if (maximalisLepes > 0)
                {
                    energiaSzukseglet = 2 * maximalisLepes * maximalisLepes;
                }
            }

            if (maximalisLepes == 0)
            {
                NaploLista.Items.Add($"⚠️ Nincs elég energia a mozgáshoz! (Akku: {rover.Akku})");

                // Ha nappal van, várjunk egy kicsit a töltődésre
                if (rover.IsNappal && rover.Akku < 90)
                {
                    NaploLista.Items.Add($"☀️ Várakozás töltődésre...");
                    rover.VarakozasEgyFelora(out string uzenet);
                    AllapotFrissites();
                }

                await Task.Delay(ANIMACIOS_KESLELTETES);
                return;
            }

            // Lépésenként ellenőrizzük a mozgást
            for (int i = 0; i < maximalisLepes; i++)
            {
                int kovetkezoX = aktualisX + eltolasX;
                int kovetkezoY = aktualisY + eltolasY;

                // Ellenőrizzük, hogy a következő pozíció a pályán belül van-e és járható-e
                if (!IsJarhato(kovetkezoX, kovetkezoY))
                {
                    if (i == 0)
                    {
                        NaploLista.Items.Add($"🚫 Nem lehet mozogni: ({kovetkezoX},{kovetkezoY}) nem járható");
                    }
                    break;
                }

                // Ha van ásvány a következő mezőn, akkor itt megállunk (bányászni kell)
                string kovetkezoCella = terkep[kovetkezoX, kovetkezoY];
                if (kovetkezoCella == "B" || kovetkezoCella == "Y" || kovetkezoCella == "G")
                {
                    // Ellenőrizzük, hogy van-e elég energia a bányászathoz
                    if (rover.Akku < 2)
                    {
                        NaploLista.Items.Add($"⚠️ Nincs elég energia a bányászathoz! (Akku: {rover.Akku})");
                        break;
                    }

                    // Ha az első lépésnél ásványba ütközünk, akkor még ne mozduljunk
                    if (i == 0)
                    {
                        // Megpróbáljuk kibányászni
                        if (rover.TryBanyasz(out string uzenet))
                        {
                            terkep[kovetkezoX, kovetkezoY] = ".";
                            CellaFrissitese(kovetkezoX, kovetkezoY);
                            NaploLista.Items.Add($"{sebessegIkonok[tenylegesSebesseg]} Ásvány begyűjtve ({kovetkezoX},{kovetkezoY}): {uzenet}");
                            AsvanySzoveg.Text = rover.AsvanyokSzama.ToString();

                            // A rover nem mozdul, mert bányászott
                            AllapotFrissites();
                            await Task.Delay(ANIMACIOS_KESLELTETES);
                            return;
                        }
                        else
                        {
                            NaploLista.Items.Add($"{sebessegIkonok[tenylegesSebesseg]} Nem sikerült bányászni ({kovetkezoX},{kovetkezoY})");
                            AllapotFrissites();
                            await Task.Delay(ANIMACIOS_KESLELTETES);
                            return;
                        }
                    }
                    else
                    {
                        // Ha nem az első lépésnél van ásvány, akkor odaérve megállunk
                        aktualisX = kovetkezoX;
                        aktualisY = kovetkezoY;
                        megtettLepesek++;

                        // Bányászás
                        if (rover.TryBanyasz(out string uzenet))
                        {
                            terkep[aktualisX, aktualisY] = ".";
                            CellaFrissitese(aktualisX, aktualisY);
                            NaploLista.Items.Add($"{sebessegIkonok[tenylegesSebesseg]} Ásvány begyűjtve ({aktualisX},{aktualisY}): {uzenet}");
                            AsvanySzoveg.Text = rover.AsvanyokSzama.ToString();
                        }
                        break;
                    }
                }

                // Ha sima járható mező, akkor továbblépünk
                aktualisX = kovetkezoX;
                aktualisY = kovetkezoY;
                megtettLepesek++;
            }

            // Ha nem mozdultunk, nincs mit frissíteni
            if (megtettLepesek == 0)
            {
                await Task.Delay(ANIMACIOS_KESLELTETES);
                return;
            }

            // Régi pozíció frissítése
            terkep[roverX, roverY] = ".";
            CellaFrissitese(roverX, roverY);

            // Új pozíció beállítása
            int regiX = roverX;
            int regiY = roverY;
            roverX = aktualisX;
            roverY = aktualisY;

            // Rover logika frissítése (energia, idő)
            int elmozdulasX = roverX - regiX;
            int elmozdulasY = roverY - regiY;
            bool siker = false;

            if (elmozdulasX != 0 || elmozdulasY != 0)
            {
                // A tényleges sebesség kiszámítása a megtett lépések alapján
                int tenylegesLepesSzam = Math.Max(Math.Abs(elmozdulasX), Math.Abs(elmozdulasY));
                Rover.Sebesseg tenylegesSebessegErtek;

                if (tenylegesLepesSzam <= 1)
                    tenylegesSebessegErtek = Rover.Sebesseg.Lassu;
                else if (tenylegesLepesSzam <= 2)
                    tenylegesSebessegErtek = Rover.Sebesseg.Normal;
                else
                    tenylegesSebessegErtek = Rover.Sebesseg.Gyors;

                siker = rover.TryMove(elmozdulasX, elmozdulasY, tenylegesSebessegErtek, out int megtett, out string moveUzenet);

                if (!siker)
                {
                    NaploLista.Items.Add($"⚠️ {moveUzenet}");
                }
            }

            // Ha sikeres volt a gyors mozgás, növeljük a számlálót
            if (siker && tenylegesSebesseg == Rover.Sebesseg.Gyors)
            {
                gyorsSebessegSzamlalo++;
            }

            // Rover pozíció frissítése a térképen
            terkep[roverX, roverY] = "S";
            CellaFrissitese(roverX, roverY);

            // Kamera célpontjának frissítése
            kovetettCel = jatekGombok[roverX, roverY];

            NaploLista.Items.Add($"{sebessegIkonok[tenylegesSebesseg]} Mozgás: {megtettLepesek}/{lepesSzam} lépés {tenylegesSebesseg} sebességgel → ({roverX},{roverY}) (Akku: {rover.Akku})");

            AllapotFrissites();

            // Animációs késleltetés a simább mozgásért
            await Task.Delay(ANIMACIOS_KESLELTETES);
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
            if (lblAkkumulator != null)
                lblAkkumulator.Text = $"🔋 Akkumulátor: {rover.Akku}/100";

            if (lblIdo != null)
            {
                string idoString = rover.NapszakString ?? $"{rover.FeloraTick / 2}:{(rover.FeloraTick % 2 * 30).ToString("D2")}";
                string napszak = rover.IsNappal ? "☀️ Nappal" : "🌙 Éjszaka (csak lassú)";
                string sebessegIkon = sebessegIkonok[rover.AktualisSebesseg];
                string energiaszin = rover.Akku < 20 ? "⚠️ KRITIKUS" : (rover.Akku < 50 ? "⚡ Alacsony" : "✅ Optimális");
                lblIdo.Text = $"⏰ {idoString} ({napszak}) {sebessegIkon} {energiaszin}";
            }

            if (lblPozicio != null)
                lblPozicio.Text = $"📍 Pozíció: {rover.X},{rover.Y}";
            if (lblLepesek != null)
                lblLepesek.Text = $"👣 Lépések: {rover.LepesSzam}";
            if (lblAsvanyok != null)
                lblAsvanyok.Text = $"💎 Kibányászott ásványok: {rover.AsvanyokSzama}";
        }

        // Bányászási kísérlet a rover jelenlegi pozícióján
        private async void BanyaszasAktualisPozicion()
        {
            // Csak akkor engedélyezett a bányászás, ha van ásvány szimbólum a rover alatt: B, Y, G
            string szimbolum = terkep[roverX, roverY];
            if (szimbolum != "B" && szimbolum != "Y" && szimbolum != "G")
            {
                NaploLista.Items.Add("❌ Nincs itt ásvány.");
                return;
            }

            string uzenet;
            bool siker = rover.TryBanyasz(out uzenet);
            NaploLista.Items.Add(siker ? $"✅ {uzenet}" : $"❌ {uzenet}");

            if (siker)
            {
                // Ásvány eltávolítása a térképről és cella frissítése
                terkep[roverX, roverY] = "S"; // rover az S-en áll
                CellaFrissitese(roverX, roverY);
                AsvanySzoveg.Text = rover.AsvanyokSzama.ToString();
            }

            AllapotFrissites();
            await Task.Delay(ANIMACIOS_KESLELTETES);
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
                NaploLista.Items.Add($"❌ Nincs elérhető útvonal ({celX},{celY})!");
                NaploLista.ScrollIntoView(NaploLista.Items[NaploLista.Items.Count - 1]);
                return false;
            }

            int tavolsag = utvonal.Count - 1; // Első elem a jelenlegi pozíció

            NaploLista.Items.Add($"🛣️ Útvonal hossza: {tavolsag} lépés");
            NaploLista.ScrollIntoView(NaploLista.Items[NaploLista.Items.Count - 1]);

            for (int i = 0; i < utvonal.Count - 1; i++)
            {
                var aktualis = utvonal[i];
                var kovetkezo = utvonal[i + 1];

                int dx = kovetkezo.X - aktualis.X;
                int dy = kovetkezo.Y - aktualis.Y;

                // Számoljuk a hátralévő távolságot
                int hatralevoTav = utvonal.Count - i - 1;

                // Optimalis sebesség választás
                Rover.Sebesseg valasztottSebesseg = rover.OptimalisSebessegValasztas(hatralevoTav);

                // Ellenőrizzük, hogy van-e ásvány a következő mezőn
                string kovetkezoCella = terkep[kovetkezo.X, kovetkezo.Y];
                bool asvanyEloterben = kovetkezoCella == "B" || kovetkezoCella == "Y" || kovetkezoCella == "G";

                // Ha ásvány van előttünk, lassítsunk le
                if (asvanyEloterben && valasztottSebesseg != Rover.Sebesseg.Lassu)
                {
                    valasztottSebesseg = Rover.Sebesseg.Lassu;
                    NaploLista.Items.Add($"🐢 Lassítás: ásvány előttünk ({kovetkezo.X},{kovetkezo.Y})");
                    NaploLista.ScrollIntoView(NaploLista.Items[NaploLista.Items.Count - 1]);
                }

                // Mozgás a kiválasztott sebességgel
                int eltolasX = kovetkezo.X - roverX;
                int eltolasY = kovetkezo.Y - roverY;

                // Megjegyezzük a régi pozíciót
                int regiX = roverX;
                int regiY = roverY;

                // Mozgatás
                RoverMozgatasa(eltolasX, eltolasY, valasztottSebesseg);

                // Ellenőrizzük, hogy tényleg mozdult-e
                if (roverX == regiX && roverY == regiY)
                {
                    // Ha nem mozdult, próbáljuk újra lassabban
                    if (valasztottSebesseg != Rover.Sebesseg.Lassu)
                    {
                        NaploLista.Items.Add($"🔄 Újrapróbálkozás lassabb sebességgel...");
                        RoverMozgatasa(eltolasX, eltolasY, Rover.Sebesseg.Lassu);

                        if (roverX == regiX && roverY == regiY)
                        {
                            NaploLista.Items.Add($"❌ Nem sikerült mozogni ({kovetkezo.X},{kovetkezo.Y}) felé");
                            NaploLista.ScrollIntoView(NaploLista.Items[NaploLista.Items.Count - 1]);
                            return false;
                        }
                    }
                    else
                    {
                        NaploLista.Items.Add($"❌ Nem sikerült mozogni ({kovetkezo.X},{kovetkezo.Y}) felé");
                        NaploLista.ScrollIntoView(NaploLista.Items[NaploLista.Items.Count - 1]);
                        return false;
                    }
                }

                AllapotFrissites();
            }

            return true;
        }

        private async Task OsszesAsvanyGyujteseAsync()
        {
            NaploLista.Items.Add("=== ÁSVÁNYGYŰJTÉS KEZDÉSE ===");
            NaploLista.ScrollIntoView(NaploLista.Items[NaploLista.Items.Count - 1]);

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

            NaploLista.Items.Add($"🔍 Összesen {asvanyok.Count} ásvány található");
            NaploLista.ScrollIntoView(NaploLista.Items[NaploLista.Items.Count - 1]);

            int osszes = asvanyok.Count;
            int energiaFigyelo = 0;

            // 2️⃣ Amíg van ásvány a listában
            while (asvanyok.Count > 0)
            {
                // Ha nagyon kevés az energia, várjunk a töltődésre
                if (rover.Akku < 5 && rover.IsNappal)
                {
                    NaploLista.Items.Add($"☀️ Energiatakarékos várakozás (Akku: {rover.Akku})...");
                    await Task.Delay(ANIMACIOS_KESLELTETES * 2);
                    rover.VarakozasEgyFelora(out string _);
                    AllapotFrissites();
                    continue;
                }

                // 2a️⃣ Legközelebbi ásvány kiválasztása (Manhattan távolság)
                var kovetkezoCel = asvanyok.OrderBy(m => Math.Abs(m.X - roverX) + Math.Abs(m.Y - roverY)).First();

                int tavolsag = Math.Abs(kovetkezoCel.X - roverX) + Math.Abs(kovetkezoCel.Y - roverY);
                NaploLista.Items.Add($"🎯 Cél: ({kovetkezoCel.X},{kovetkezoCel.Y}) - Távolság: {tavolsag} (Akku: {rover.Akku})");
                NaploLista.ScrollIntoView(NaploLista.Items[NaploLista.Items.Count - 1]);

                // 2b️⃣ Útvonal keresés a célhoz
                bool sikerult = await UtkeresesCelhozAsync(kovetkezoCel.X, kovetkezoCel.Y);

                if (sikerult)
                {
                    // 2c️⃣ Felszedett ásvány eltávolítása a listából
                    asvanyok.RemoveAll(m => m.X == roverX && m.Y == roverY);
                    NaploLista.Items.Add($"✅ Begyűjtve! ({lblAsvanyok.Text}/{osszes}) (Akku: {rover.Akku})");
                    NaploLista.ScrollIntoView(NaploLista.Items[NaploLista.Items.Count - 1]);
                    energiaFigyelo = 0;
                }
                else
                {
                    energiaFigyelo++;

                    // Ha többször nem sikerült, lehet hogy nincs elég energia
                    if (energiaFigyelo > 3)
                    {
                        if (rover.IsNappal)
                        {
                            NaploLista.Items.Add($"☀️ Hosszabb várakozás töltődésre...");
                            for (int i = 0; i < 5; i++)
                            {
                                rover.VarakozasEgyFelora(out string _);
                                await Task.Delay(ANIMACIOS_KESLELTETES);
                            }
                            AllapotFrissites();
                            energiaFigyelo = 0;
                            continue;
                        }
                        else
                        {
                            // Ha éjszaka van és nem sikerül, lehet hogy ez az ásvány elérhetetlen
                            NaploLista.Items.Add($"⚠️ Elérhetetlen ásvány, kihagyás...");
                            asvanyok.Remove(kovetkezoCel);
                            energiaFigyelo = 0;
                        }
                    }
                    else
                    {
                        // Ha nem sikerült, próbáljuk újra
                        await Task.Delay(ANIMACIOS_KESLELTETES);
                    }
                }
            }

            NaploLista.Items.Add($"=== ÁSVÁNYGYŰJTÉS BEFEJEZVE === Összesen {lblAsvanyok} ásványt gyűjtöttünk! (Maradék energia: {rover.Akku})");
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