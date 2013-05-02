using System;
using System.Text.RegularExpressions;

using log4net;

namespace EQEmulator.Servers.Internals.Entities
{
    internal abstract class Entity : IEquatable<Entity>
    {
        protected static readonly ILog _log = LogManager.GetLogger(typeof(Entity));

        private int _id;
        protected string _name, _surname;
        private string _displayName = string.Empty;
        private bool _dePopped = false;
        private float _xPos = 0.0F, _yPos = 0.0F, _zPos = 0.0F, _heading = 0.0F;

        internal abstract void CheckCoordLosNoZLeaps(float curX, float curY, float curZ, float targetX, float targetY, float targetZ, float perWalk);
        internal abstract bool Process();
        internal abstract void Save();

        public Entity(int id, string name) : this(id, name, string.Empty)
        { }

        public Entity(int id, string name, string surName)
        {
            _id = id;
            this.Name = name;
            this.Surname = surName;
        }

        public Entity(int id, string name, string surName, float xPos, float yPos, float zPos, float heading)
        {
            this.ID = id;
            this.Name = name;
            this.Surname = surName;
            this.X = xPos;
            this.Y = yPos;
            this.Z = zPos;
            this.Heading = heading;
        }

        internal int ID
        {
            get { return _id; }
            set { _id = value; }
        }

        internal virtual string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        internal virtual string Surname
        {
            get { return _surname; }
            set { _surname = value; }
        }

        internal bool IsDePopped
        {
            get { return _dePopped; }
        }

        internal virtual float Heading
        {
            get { return _heading; }
            set { _heading = value; }
        }

        internal virtual float X
        {
            get { return _xPos; }
            set { _xPos = value; }
        }

        internal virtual float Y
        {
            get { return _yPos; }
            set { _yPos = value; }
        }

        internal virtual float Z
        {
            get { return _zPos; }
            set { _zPos = value; }
        }

        internal virtual bool IsAttackable
        {
            get { throw new NotImplementedException(); }
        }

        internal string DisplayName
        {
            get
            {
                if (_displayName == string.Empty) {
                    _displayName = this.Name.Replace('_', ' ');   // first replace underscores with spaces
                    _displayName = Regex.Replace(_displayName, "[^a-zA-Z ]", "");    // then strip any non-alpha chars (except spaces, yo)
                }

                return _displayName;
            }
        }

        /// <summary></summary>
        internal virtual void DePop()
        {
            _dePopped = true;
            //_log.DebugFormat("{0} with id {1} has depopped.", this.Name, this.ID);
        }

        #region IEquatable<Entity> Members

        public bool Equals(Entity other)
        {
            if (other == null)
                return false;

            if (GetType() != other.GetType())
                return false;

            return this.ID == other.ID && this.Name == other.Name;
        }

        #endregion

        public override bool Equals(object obj)
        {
            Entity entity = obj as Entity;
            if (entity != null)
                return Equals(entity);  // forward to the strongly typed Equals()
            else
                return false;
        }

        public override int GetHashCode()
        {
            return 397 * _name.GetHashCode() ^ _id;
        }

        public override string ToString()
        {
            return this.Name + this.ID.ToString();
        }

        public static bool operator ==(Entity entity1, Entity entity2)
        {
            return Object.Equals(entity1, entity2);
        }

        public static bool operator !=(Entity entity1, Entity entity2)
        {
            return !Object.Equals(entity1, entity2);
        }
    }
}