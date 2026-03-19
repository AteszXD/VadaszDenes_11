using System;

namespace Bemutato.Assetts
{
    internal class Rover
    {
        #region Mezők
        private const int AkkuKapacitas = 100;
        private const int K = 2; // fogyasztási konstans: E = k * v^2 (jelenleg nem használjuk a mozgáskorlátozásnál)
        private const int NappalOrak = 16; // 16 óra nappali
        private const int EjszakaOrak = 8;  // 8 óra éjszakai
        private const int NappalFelorak = NappalOrak * 2;
        private const int EjszakaFelorak = EjszakaOrak * 2;
        private const int CiklusFelorak = NappalFelorak + EjszakaFelorak; // 48 félóra (24 óra)

        // Dinamikus időkeret
        private int maximalalisFeloraTick; // Az elérhető időkeret félórákban
        #endregion

        #region Tulajdonságok
        // Pozíció a rácson (blokk koordináták)
        public int X { get; private set; }
        public int Y { get; private set; }
        // Akkumulátor állapota (0..100)
        public int Akku { get; private set; } = AkkuKapacitas;
        // Idő mérése félóra tickekben a kezdés óta (0..)
        public int FeloraTick { get; private set; }
        // Statisztikák
        public int LepesSzam { get; private set; }
        public int AsvanyokSzama { get; private set; }
        // Aktuális sebesség
        public Sebesseg AktualisSebesseg { get; set; } = Sebesseg.Normal;
        #endregion

        public enum Sebesseg
        {
            Lassu = 1,   // 1 blokk / félóra
            Normal = 2,  // 2 blokk / félóra
            Gyors = 3    // 3 blokk / félóra
        }

        public Rover(int eloirhetoIdokeretOrak = 24)
        {
            if (eloirhetoIdokeretOrak < 24)
            {
                throw new ArgumentException(
                    $"Az elérhető időkeret legalább 24 óra kell legyen. Megadott érték: {eloirhetoIdokeretOrak} óra.",
                    nameof(eloirhetoIdokeretOrak));
            }

            maximalalisFeloraTick = eloirhetoIdokeretOrak * 2;

            X = 0;
            Y = 0;
            Akku = AkkuKapacitas;
            FeloraTick = 0;
            LepesSzam = 0;
            AsvanyokSzama = 0;
            AktualisSebesseg = Sebesseg.Normal;
        }

        public bool IsNappal
        {
            get
            {
                int t = FeloraTick % CiklusFelorak;
                int nappalKezdet = 4 * 2; // 4:00 -> 8 félóra
                int nappalVeg = nappalKezdet + NappalFelorak; // 8 + 32 = 40 -> 20:00
                return t >= nappalKezdet && t < nappalVeg;
            }
        }

        public bool IdokeretKifogyott => FeloraTick >= maximalalisFeloraTick;
        public int ElerhetoIdokeretOrak => maximalalisFeloraTick / 2;
        public int HatralevoIdoOrak => Math.Max(0, (maximalalisFeloraTick - FeloraTick) / 2);
        public int FeloraIndexANapban => FeloraTick % CiklusFelorak;

        public TimeSpan NapszakIdo
        {
            get
            {
                int t = FeloraIndexANapban;
                int orak = t / 2;
                int percek = (t % 2) * 30;
                return new TimeSpan(orak, percek, 0);
            }
        }

        public string NapszakString
        {
            get
            {
                var ts = NapszakIdo;
                return $"{ts.Hours:D2}:{ts.Minutes:D2}";
            }
        }

        // Egyszerű szabályok alapján döntött sebesség:
        // - ha van érc <= 3 blokk távolságra -> sebesség = 1
        // - ha van érc >3 és <=5 blokk távolságra -> sebesség = 2
        // - ha minden érc távolabb van, vagy nincs érc -> sebesség = 3
        private Sebesseg DetermineSpeedByMineralProximity(VadaszDenes.SimplePathfinder pathfinder)
        {
            var nearest = pathfinder?.FindNearestMineral(X, Y);
            if (nearest == null)
                return Sebesseg.Gyors; // nincs érc -> leggyorsabb

            int dx = Math.Abs(nearest.Value.Item1 - X);
            int dy = Math.Abs(nearest.Value.Item2 - Y);

            // Használjuk a chebyshev távolságot (rácsos "radius"): max(dx,dy)
            int chebyshev = Math.Max(dx, dy);

            if (chebyshev <= 3)
                return Sebesseg.Lassu;
            if (chebyshev <= 5)
                return Sebesseg.Normal;
            return Sebesseg.Gyors;
        }

        // Új, egyszerűsített mozgásmódszer: a sebességet a környező érc alapján határozza meg.
        // A módszer figyelmen kívül hagyja a korábbi energia-alapú korlátozásokat (eredeti viselkedés felülírva).
        // Visszatér: igaz, ha legalább egy lépést megtettünk; out megtettLepesek az elmozdult blokkok száma.
        public bool TryMoveAdaptive(VadaszDenes.SimplePathfinder pathfinder, int dx, int dy, out int megtettLepesek, out string uzenet)
        {
            megtettLepesek = 0;
            uzenet = "";

            if (IdokeretKifogyott)
            {
                uzenet = "Az elérhető időkeret kifogyott!";
                return false;
            }

            // Ha nincs mozgásirány, nem mozdulunk
            if (dx == 0 && dy == 0)
            {
                uzenet = "A rover nem mozdult (azonos pozíció).";
                return true;
            }

            // Döntsük el az aktuális sebességet a közelben lévő érc alapján
            var chosenSpeed = DetermineSpeedByMineralProximity(pathfinder);
            AktualisSebesseg = chosenSpeed;
            int maxSteps = (int)chosenSpeed;

            // Normalizált lépésirány (átlós megengedett)
            int stepX = Math.Sign(dx);
            int stepY = Math.Sign(dy);

            // Végrehajtjuk a mozgást: lépésenként lépünk, összesen maxSteps-t
            int stepsDone = 0;
            for (int i = 0; i < maxSteps; i++)
            {
                X += stepX;
                Y += stepY;
                stepsDone++;
            }

            LepesSzam += stepsDone;
            megtettLepesek = stepsDone;

            // Egyszerűsítve: nem változtatunk Akkun, csak halad az idő
            FeloraTick++;

            uzenet = $"{stepsDone} lépés megtéve. Sebesség: {chosenSpeed}";
            return stepsDone > 0;
        }

        // A többi eredeti művelet változatlan maradt (bányászat, várakozás stb.)

        public bool TryBanyasz(out string uzenet)
        {
            if (IdokeretKifogyott)
            {
                uzenet = "Az elérhető időkeret kifogyott!";
                return false;
            }

            int banyaszFogyasztas = 2; // félóránként bányászat közben
            int toltesEzFelora = IsNappal ? 10 : 0;
            int netto = -banyaszFogyasztas + toltesEzFelora;

            if (Akku + netto < 0)
            {
                uzenet = "Nincs elég akkumulátor a bányászathoz ebben a félórában.";
                return false;
            }

            Akku += netto;
            AkkuKorlatozas();

            AsvanyokSzama++;
            FeloraTick++;

            uzenet = "Egy ásványblokk kibányászva.";
            return true;
        }

        public void VarakozasEgyFelora(out string uzenet)
        {
            if (IdokeretKifogyott)
            {
                uzenet = "Az elérhető időkeret kifogyott!";
                return;
            }

            int keszenletFogyasztas = 1;
            int toltesEzFelora = IsNappal ? 10 : 0;
            int netto = -keszenletFogyasztas + toltesEzFelora;

            Akku += netto;
            AkkuKorlatozas();

            FeloraTick++;
            uzenet = "Várakozás egy félóráig.";
        }

        private void AkkuKorlatozas()
        {
            if (Akku < 0) Akku = 0;
            if (Akku > AkkuKapacitas) Akku = AkkuKapacitas;
        }

        public void Reset(int kezdoX = 0, int kezdoY = 0, int kezdoAkku = AkkuKapacitas, int kezdoTick = 0)
        {
            X = kezdoX;
            Y = kezdoY;
            Akku = Math.Max(0, Math.Min(AkkuKapacitas, kezdoAkku));
            FeloraTick = Math.Max(0, kezdoTick);
            LepesSzam = 0;
            AsvanyokSzama = 0;
            AktualisSebesseg = Sebesseg.Normal;
        }
    }
}