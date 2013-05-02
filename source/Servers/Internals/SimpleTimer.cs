using System;

namespace EQEmulator.Servers.Internals
{
    /// <summary>A simple class to keep track of set time intervals</summary>
    /// <remarks>This timer is not exact in its timings. The internal time is reset to the time of a successful check, not incremented
    /// with the interval.</remarks>
    internal class SimpleTimer
    {
        private int _interval;
        private DateTime _startTime;
        private bool _enabled;

        /// <summary>Starts the timer in the stopped state with the specified interval.</summary>
        /// <param name="interval">Amount of miliseconds until timer is triggered.  A zero value initializes the timer in an unstarted state.</param>
        internal SimpleTimer(int interval)
            : this(interval, false) {}

        /// <summary>Initializes and (normally) starts the timer instance.</summary>
        /// <param name="interval">Amount of miliseconds until timer is triggered.  A zero value initializes the timer in an unstarted state.</param>
        /// <param name="startTriggered">Whether or not the timer starts in a triggered state.</param>
        internal SimpleTimer(int interval, bool startTriggered)
        {
            if (startTriggered)
                _startTime = DateTime.Now.Subtract(TimeSpan.FromMilliseconds(interval));
            else
                _startTime = DateTime.Now;

            _enabled = interval == 0 ? false : true;
            _interval = interval;
        }

        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        public int Interval
        {
            get { return _interval; }
        }

        /// <summary>Checks if the timer interval has elapsed, reseting the timer if check will return true.</summary>
        /// <returns>True if the timer interval has elapsed, else False.</returns>
        internal bool Check()
        {
            return Check(true);
        }

        /// <summary>Checks if the timer interval has elapsed with optional reset specifier.</summary>
        /// <param name="resetIfElapsed">Specifies whether of not to reset the timer if the check will return true.</param>
        /// <returns>True if the timer interval has elapsed, else False.</returns>
        private bool Check(bool resetIfElapsed)
        {
            if (!_enabled)
                return false;

            if (DateTime.Now.Subtract(_startTime).TotalMilliseconds > _interval)
            {
                if (resetIfElapsed)
                    _startTime = DateTime.Now;

                return true;
            }
            else
                return false;
        }

        internal bool Peek()
        {
            return Check(false);
        }

        /// <summary>Starts or restarts the timer with the default interval.</summary>
        internal void Start()
        {
            this.Start(_interval);
        }

        /// <summary>Starts or restarts the timer with a specified milisecond interval and in an un-triggered state.</summary>
        internal void Start(int interval)
        {
            this.Start(interval, false);
        }

        /// <summary>Starts or restarts the timer, optionally in a triggered state, with a specified milisecond interval.</summary>
        internal void Start(int interval, bool startTriggered)
        {
            if (startTriggered)
                _startTime = DateTime.Now.Subtract(TimeSpan.FromMilliseconds(interval));
            else
                _startTime = DateTime.Now;

            _interval = interval;
            _enabled = true;
        }

        internal void Stop()
        {
            _enabled = false;
        }

        /// <summary>Returns the total amount of miliseconds until the timer will have elapsed.</summary>
        internal TimeSpan GetRemainingTime()
        {
            if (Peek())
                return new TimeSpan(0);
            else
                return TimeSpan.FromMilliseconds(DateTime.Now.Subtract(_startTime).TotalMilliseconds - _interval);
        }
    }
}
