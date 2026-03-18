using System;

namespace Bemutato.Assetts
{
    internal class Rover
    {
        #region Mezők
        private const int AkkuKapacitas = 100;
        private const int K = 2; // fogyasztási konstans: E = k * v^2
        private const int NappalFelorak = 16 * 2; // 16 óra -> félóra egységek
        private const int EjszakaFelorak = 8 * 2; // 8 óra -> félóra egységek
        private const int CiklusFelorak = NappalFelorak + EjszakaFelorak; // 48 félóra
        #endregion

        #region Tulajdonságok
        // Pozíció a rácson (blokk koordináták)
        public int X { get; private set; }
        public int Y { get; private set; }
        // Akkumulátor állapota (0..100)
        public int Akku { get; private set; } = AkkuKapacitas;
        // Idő mérése félóra tickekben a kezdés óta (0..)
        // 0..31 => nappal (első 32 félóra), 32..47 => éjszaka (következő 16 félóra)
        public int FeloraTick { get; private set; }
        // Statisztikák
        public int LepesSzam { get; private set; }
        public int AsvanyokSzama { get; private set; }
        #endregion

        public enum Sebesseg
        {
            Lassu = 1,   // 1 blokk / félóra
            Normal = 2,  // 2 blokk / félóra
            Gyors = 3    // 3 blokk / félóra
        }

        public Rover()
        {
            X = 0;
            Y = 0;
            Akku = AkkuKapacitas;
            FeloraTick = 0;
            LepesSzam = 0;
            AsvanyokSzama = 0;
        }

        // Hogy éppen nappal (true) vagy éjszaka (false) van-e
        public bool IsNappal
        {
            get
            {
                int t = FeloraTick % CiklusFelorak; // 0..47 félóra indexek 00:00-tól kezdve
                int nappalKezdet = 4 * 2; // 4:00 -> 8 félóra
                int nappalVeg = nappalKezdet + NappalFelorak; // 8 + 32 = 40 -> 20:00
                return t >= nappalKezdet && t < nappalVeg;
            }
        }

        // Visszaadja a becsomagolt félóra indexet a 24 órás ciklusban (0..47)
        public int FeloraIndexANapban => FeloraTick % CiklusFelorak;

        // Visszaadja a napszakot reprezentáló TimeSpan-t (24 órára csomagolva)
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

        // Visszaadja a formázott időstringet "HH:mm" formátumban a UI számára (23:30 után 00:00-ra ugrik)
        public string NapszakString
        {
            get
            {
                var ts = NapszakIdo;
                return $"{ts.Hours:D2}:{ts.Minutes:D2}";
            }
        }

        // Mozgási kísérlet az (dx,dy) irányba. dx és dy lépésenkénti iránykomponensek.
        // Lehet átlósan mozogni. Minden hívás egy félórás tevékenységet végez és legfeljebb 'sebesseg' lépést próbál.
        // Igazat ad vissza, ha legalább egy lépést végrehajtott.
        // out megtettLepesek: ténylegesen megtett blokkok száma ebben a félórában (0..(int)sebesseg)
        // out uzenet: leírás ha a művelet sikertelen vagy részleges
        public bool TryMove(int dx, int dy, Sebesseg sebesseg, out int megtettLepesek, out string uzenet)
        {
            megtettLepesek = 0;
            uzenet = "";

            int vKert = (int)sebesseg;
            if (dx == 0 && dy == 0)
            {
                uzenet = "Az irány nem lehet nulla.";
                return false;
            }

            // Lépésenkénti irány normalizálása -1/0/1 értékekre minden tengelyen (átlós megengedett)
            int lepesX = Math.Sign(dx);
            int lepesY = Math.Sign(dy);

            int toltesEzFelora = IsNappal ? 10 : 0;

            // Maximális megengedett sebesség keresése (vJelolt <= vKert) úgy, hogy az akku ne menjen negatívba a félóra után
            int vEngedelyezett = 0;
            for (int vJelolt = vKert; vJelolt >= 1; vJelolt--)
            {
                int fogyasztas = K * vJelolt * vJelolt;
                int netto = -fogyasztas + toltesEzFelora;
                if (Akku + netto >= 0)
                {
                    vEngedelyezett = vJelolt;
                    break;
                }
            }

            if (vEngedelyezett == 0)
            {
                uzenet = "Nincs elég akkumulátor egy lépés megtételéhez ebben a félórában.";
                // Ha nem tud mozogni, az idő akkor is telik, ha várakozást akarunk modellezni; itt nem változtatunk állapotot és false-t adunk vissza
                return false;
            }

            // Mozgás alkalmazása: vEngedelyezett lépés mozgás az irányba
            X += lepesX * vEngedelyezett;
            Y += lepesY * vEngedelyezett;
            LepesSzam += vEngedelyezett;

            // Akkumulátor változás alkalmazása a félórára
            int tenylegesFogyasztas = K * vEngedelyezett * vEngedelyezett;
            Akku = Akku - tenylegesFogyasztas + toltesEzFelora;
            AkkuKorlatozas();

            // Idő előrehaladása egy félórával
            FeloraTick++;

            if (vEngedelyezett < vKert)
                uzenet = $"Részleges mozgás: kért {vKert} lépés, de {vEngedelyezett} lépés történt akkumulátor korlátok miatt.";
            else
                uzenet = $"{vEngedelyezett} lépés megtéve {sebesseg} sebességgel.";

            megtettLepesek = vEngedelyezett;
            return true;
        }

        // Bányászati kísérlet a jelenlegi pozícióban. A bányászat egy félórát vesz igénybe és a rover a blokkon áll.
        // Igazat ad vissza, ha a bányászat sikeres (egy ásványblokk begyűjtve)
        public bool TryBanyasz(out string uzenet)
        {
            int banyaszFogyasztas = 2; // félóránként bányászat közben
            int toltesEzFelora = IsNappal ? 10 : 0;
            int netto = -banyaszFogyasztas + toltesEzFelora;

            if (Akku + netto < 0)
            {
                uzenet = "Nincs elég akkumulátor a bányászathoz ebben a félórában.";
                return false;
            }

            // Bányászat energiát fogyaszt és begyűjt egy ásványblokkot
            Akku += netto;
            AkkuKorlatozas();

            AsvanyokSzama++;
            // Bányászat helyben állásnak számít; LepesSzam nem változik

            FeloraTick++;

            uzenet = "Egy ásványblokk kibányászva.";
            return true;
        }

        // Várakozás/üzemkészenlét egy félórára (nincs bányászat, nincs mozgás)
        // 1 egység fogyasztás félóránként, de +10 töltés ha nappal van
        public void VarakozasEgyFelora(out string uzenet)
        {
            int keszenletFogyasztas = 1;
            int toltesEzFelora = IsNappal ? 10 : 0;
            int netto = -keszenletFogyasztas + toltesEzFelora;

            Akku += netto;
            AkkuKorlatozas();

            FeloraTick++;
            uzenet = "Várakozás egy félóráig.";
        }

        // Segédfüggvény az akkumulátor [0, AkkuKapacitas] tartományban tartásához
        private void AkkuKorlatozas()
        {
            if (Akku < 0) Akku = 0;
            if (Akku > AkkuKapacitas) Akku = AkkuKapacitas;
        }

        // Rover állapotának visszaállítása (tesztekhez)
        public void Reset(int kezdoX = 0, int kezdoY = 0, int kezdoAkku = AkkuKapacitas, int kezdoTick = 0)
        {
            X = kezdoX;
            Y = kezdoY;
            Akku = Math.Max(0, Math.Min(AkkuKapacitas, kezdoAkku));
            FeloraTick = Math.Max(0, kezdoTick);
            LepesSzam = 0;
            AsvanyokSzama = 0;
        }
    }
}