namespace ValloxSerialNet
{
    /// <summary>
    /// Contains one single variable.
    /// </summary>
    public class ValloxVariable : NotificationObject
    {
        private byte _value = 0;
        private int _counter = 1;

        public ValloxVariable(Vallox.Variable.VariableNumber id, string description)
        {
            Id = id;
            Description = description;
        }

        public string Description { get; private set; }
        public Vallox.Variable.VariableNumber Id { get; private set; }

        public byte Value
        {
            get
            {
                return _value;
            }

            set
            {
                if (value != _value)
                {
                    _value = value;
                    RaisePropertyChanged("Value");
                }

                OnValueChanged();
            }
        }

        private void OnValueChanged()
        {
            Counter++;
        }

        public int Counter
        {
            get
            {
                return _counter;
            }

            set
            {
                if (value != _counter)
                {
                    _counter = value;
                    RaisePropertyChanged("Counter");
                }
            }
        }
    }
}
