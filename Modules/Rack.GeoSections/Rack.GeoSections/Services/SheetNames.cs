namespace Rack.GeoSections.Services
{
    public class SheetNames
    {
        public sealed class Wells
        {
            public const string SheetName = "Скважины";
            public const string Name = "Скважина";
            public const string Altitude = "Альтитуда";
            public const string X = "X";
            public const string Y = "Y";
            public const string Bottom = "Забой";
            public const string IsEnabled = "Учитывается при построении";
        }

        public sealed class GeophysicalData
        {
            public const string SheetName = "Геофизика";
            public const string WellName = "Скважина";
            public const string Roof = "Кровля";
            public const string Sole = "Подошва";
            public const string Value = "ПС";
        }

        public sealed class DecorationColumns
        {
            public const string SheetName = "Колонки";
            public const string Header = "Заголовок";
            public const string Text = "Текст";
            public const string TopLeft = "Верх (слева)";
            public const string BottomLeft = "Низ (слева)";
            public const string TopRight = "Верх (справа)";
            public const string BottomRight = "Низ (справа)";
            public const string Mode = "Режим отображения";
        }

        public sealed class Breaks
        {
            public const string SheetName = "Разбивки";
        }

        public sealed class Settings
        {
            public const string SheetName = "Параметры построения";
            public const string VerticalScale = "Вертикальный масштаб";
            public const string HorizontalScale = "Горизонтальный масштаб";
            public const string VerticalResolution = "Вертикальная частота дисретизации";
            public const string HorizontalResolution = "Горизонтальная частота дискретизации";
            public const string Top = "Верхняя граница разреза (абс. отм.)";
            public const string Bottom = "Нижняя граница разреза (абс. отм.)";
            public const string Offset = "Дополнительный отступ от краёв разреза";
            public const string IsOffsetScaled = "Масштабировать отступ";
            public const string DecorationColumnsWidth = "Ширина колонок оформления";
            public const string DecorationHeadersHeight = "Высота заголовков колонок оформления";
            public const string DepthColumnMode = "Режим отображения шкалы глубин";
            public const string Encoding = "Кодировка файлов";
        }

        public sealed class OilBearingFormations
        {
            public const string SheetName = "Нефтеносные пласты";
            public const string TopBreak = "Верхняя разбивка";
            public const string BottomBreak = "Нижняя разбивка";
        }

        public sealed class WellLabels
        {
            public const string SheetName = "Подписи на скважинах";
            public const string Well = "Скважина";
            public const string Top = "Верхняя граница";
            public const string Bottom = "Нижняя граница";
            public const string Text = "Текст подписи";
        }

        public sealed class StructuralMaps
        {
            public const string SheetName = "Структурные карты";
        }
    }
}