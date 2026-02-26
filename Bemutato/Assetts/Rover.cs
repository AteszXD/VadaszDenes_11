using System;

namespace Bemutato.Assetts
{
    internal class Rover
    {
        #region Fields
        private const int BatteryCapacity = 100;
        private const int K = 2; // consumption constant: E = k * v^2
        private const int DayHalfHours = 16 * 2; // 16 hours -> half-hour units
        private const int NightHalfHours = 8 * 2; // 8 hours -> half-hour units
        private const int CycleHalfHours = DayHalfHours + NightHalfHours; // 48 half-hours
        #endregion

        #region Properties
        // Position on grid (block coordinates)
        public int X { get; private set; }
        public int Y { get; private set; }
        // Battery state (0..100)
        public int Battery { get; private set; } = BatteryCapacity;
        // Time measured in half-hour ticks since start (0..)
        // 0..31 => day (first 32 half-hours), 32..47 => night (next 16 half-hours)
        public int HalfHourTick { get; private set; }
        // Statistics
        public int StepsMoved { get; private set; }
        public int MineralsMined { get; private set; }
        #endregion

        public enum Speed
        {
            Slow = 1,   // 1 block / half-hour
            Normal = 2, // 2 blocks / half-hour
            Fast = 3    // 3 blocks / half-hour
        }

        public Rover()
        {
            X = 0;
            Y = 0;
            Battery = BatteryCapacity;
            HalfHourTick = 0;
            StepsMoved = 0;
            MineralsMined = 0;
        }

        // Whether it's currently day (true) or night (false)
        public bool IsDay => (HalfHourTick % CycleHalfHours) < DayHalfHours;

        // Try to move in the direction (dx,dy). dx and dy are interpreted per-step direction components.
        // Allowed to move diagonally. Each call performs one half-hour of activity and attempts up to 'speed' steps.
        // Returns true if at least one step was executed.
        // out stepsMoved: number of blocks actually moved this half-hour (0..(int)speed)
        // out message: description if action failed or partial
        public bool TryMove(int dx, int dy, Speed speed, out int stepsMoved, out string message)
        {
            stepsMoved = 0;
            message = "";

            int vRequested = (int)speed;
            if (dx == 0 && dy == 0)
            {
                message = "Direction must be non-zero.";
                return false;
            }

            // Normalize per-step direction to -1/0/1 for each axis (diagonal allowed)
            int stepX = Math.Sign(dx);
            int stepY = Math.Sign(dy);

            int chargeThisHalfHour = IsDay ? 10 : 0;

            // Find the maximum allowed speed (vCandidate <= vRequested) such that battery won't go negative after the half-hour
            int vAllowed = 0;
            for (int vCandidate = vRequested; vCandidate >= 1; vCandidate--)
            {
                int consumption = K * vCandidate * vCandidate;
                int net = -consumption + chargeThisHalfHour;
                if (Battery + net >= 0)
                {
                    vAllowed = vCandidate;
                    break;
                }
            }

            if (vAllowed == 0)
            {
                message = "Insufficient battery to move any steps this half-hour.";
                // Even if can't move, time still progresses if you want to model waiting; here we don't change state and return false.
                return false;
            }

            // Apply movement: move vAllowed steps along direction
            X += stepX * vAllowed;
            Y += stepY * vAllowed;
            StepsMoved += vAllowed;

            // Apply battery change for the half-hour
            int actualConsumption = K * vAllowed * vAllowed;
            Battery = Battery - actualConsumption + chargeThisHalfHour;
            ClampBattery();

            // Advance time by one half-hour
            HalfHourTick++;

            if (vAllowed < vRequested)
                message = $"Moved partially: requested {vRequested} steps but moved {vAllowed} due to battery limits.";
            else
                message = $"Moved {vAllowed} step(s) at speed {speed}.";

            stepsMoved = vAllowed;
            return true;
        }

        // Try to mine at current position. Mining takes one half-hour and rover stands on the block.
        // Returns true when mining succeeded (one mineral block collected).
        public bool TryMine(out string message)
        {
            int miningConsumption = 2; // per half-hour while mining
            int chargeThisHalfHour = IsDay ? 10 : 0;
            int net = -miningConsumption + chargeThisHalfHour;

            if (Battery + net < 0)
            {
                message = "Insufficient battery to mine this half-hour.";
                return false;
            }

            // Mining consumes energy and collects one mineral block
            Battery += net;
            ClampBattery();

            MineralsMined++;
            // Mining counts as standing; StepsMoved unchanged.

            HalfHourTick++;

            message = "Mined one mineral block.";
            return true;
        }

        // Wait/standby for one half-hour (no mining, no movement).
        // Consumes 1 unit per half-hour standby, but charges +10 if day.
        public void WaitOneHalfHour(out string message)
        {
            int standbyConsumption = 1;
            int chargeThisHalfHour = IsDay ? 10 : 0;
            int net = -standbyConsumption + chargeThisHalfHour;

            Battery += net;
            ClampBattery();

            HalfHourTick++;
            message = "Waited one half-hour.";
        }

        // Helper to ensure battery stays within [0, BatteryCapacity]
        private void ClampBattery()
        {
            if (Battery < 0) Battery = 0;
            if (Battery > BatteryCapacity) Battery = BatteryCapacity;
        }

        // Utility: returns a human-readable state snapshot
        public string GetStatus()
        {
            return $"Pos=({X},{Y}) Battery={Battery}/100 TimeHalfHour={HalfHourTick} ({(IsDay ? "Day" : "Night")}) StepsMoved={StepsMoved} Minerals={MineralsMined}";
        }

        // Reset rover state (for tests)
        public void Reset(int startX = 0, int startY = 0, int startBattery = BatteryCapacity, int startTick = 0)
        {
            X = startX;
            Y = startY;
            Battery = Math.Max(0, Math.Min(BatteryCapacity, startBattery));
            HalfHourTick = Math.Max(0, startTick);
            StepsMoved = 0;
            MineralsMined = 0;
        }
    }
}
