﻿using System.ComponentModel;
using System.IO;

namespace LibDat2.Types {
	public abstract class FieldDataBase<TypeOfValue> : IFieldData {
		/// <summary>
		/// <see cref="DatContainer"/> which created this instance
		/// </summary>
		public DatContainer Dat;

		/// <summary>
		/// Data of this field
		/// </summary>
		public virtual TypeOfValue Value {
			get => _Value;
			set {
				_Value = value;
				PropertyChanged(this, new(nameof(Value)));
				PropertyChanged(this, new(nameof(StringValue)));
			}
		}
		private TypeOfValue _Value;

		/// <inheritdoc/>
		object IFieldData.Value { get => Value; set => Value = (TypeOfValue)value; }

		/// <summary>
		/// <see cref="Value"/> in string representation.
		/// Equals to <see cref="ToString()"/> and <see cref="FromString(string)"/>
		/// </summary>
		public virtual string StringValue { get => ToString(); set => FromString(value); }

		public event PropertyChangedEventHandler PropertyChanged = new((o, e) => { });
		protected virtual void RaisePropertyChanged(object sender, PropertyChangedEventArgs e) => PropertyChanged(sender, e);

		/// <summary>
		/// Create an instance of <see cref="FieldDataBase{TypeOfValue}"/> with the <paramref name="value"/>
		/// </summary>
		public FieldDataBase(DatContainer dat) {
			Dat = dat;
		}

		/// <summary>
		/// Read the <see cref="Value"/> from a dat file
		/// </summary>
		public abstract void Read(BinaryReader reader);

		/// <summary>
		/// Write the <see cref="Value"/> to a dat file
		/// </summary>
		public abstract void Write(BinaryWriter writer);

		/// <summary>
		/// Read the <see cref="Value"/> from its string representation
		/// </summary>
		public abstract void FromString(string value);

		/// <summary>
		/// Get the string representation of the <see cref="Value"/>
		/// </summary>
		public override string ToString() {
			return Value?.ToString() ?? "{null}";
		}
	}
}