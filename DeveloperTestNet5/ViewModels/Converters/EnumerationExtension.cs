using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using System.Linq;
using System.Windows.Markup;
using System.ComponentModel;
using DeveloperTestNet5.i18n;

namespace DeveloperTestNet5.ViewModels.Converters
{
    public class EnumerationExtension : MarkupExtension
    {
        private Type _enumType;


        public EnumerationExtension(Type enumType)
        {
            if (enumType == null)
                throw new ArgumentNullException("enumType");

            EnumType = enumType;
        }

        public Type EnumType
        {
            get { return _enumType; }
            private set
            {
                if (_enumType == value)
                    return;

                var enumType = Nullable.GetUnderlyingType(value) ?? value;

                if (enumType.IsEnum == false)
                    throw new ArgumentException("Type must be an Enum.");

                _enumType = value;
            }
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var enumValues = Enum.GetValues(EnumType);

            return (
              from object enumValue in enumValues
              select new EnumerationMember
              {
                  Value = enumValue,
                  Description = GetDescription(enumValue)
              }).ToArray();
        }

        private string GetDescription(object enumValue)
        {
            var enumString = enumValue.ToString();
            var key = $"Enum_{EnumType.Name}_{enumString}";

            return TranslationSource.Instance[key] ?? enumString;

            //var descriptionAttribute = EnumType
            //  .GetField(enumValue.ToString())
            //  .GetCustomAttributes(typeof(DescriptionAttribute), false)
            //  .FirstOrDefault() as DescriptionAttribute;


            //return descriptionAttribute != null
            //  ? descriptionAttribute.Description
            //  : enumValue.ToString();
        }

        public class EnumerationMember
        {
            public string Description { get; set; }
            public object Value { get; set; }
        }
    }
}
