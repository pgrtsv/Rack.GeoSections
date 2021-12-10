using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Rack.CrossSectionUtils.Model;
using Rack.GeoSections.Model;
using Rack.GeoTools;
using UnitsNet;
using UnitsNet.Units;

namespace Rack.GeoSections.Services
{
    public static class ExcelEx
    {
        public static int GetCellColumnIndex(this IXLRangeBase range,
            Func<IXLCell, bool> predicate)
        {
            var foundRange = range.CellsUsed(predicate);
            if (foundRange.Count() != 1)
                return -1;
            return foundRange
                .First()
                .WorksheetColumn()
                .ColumnNumber();
        }

        public static int GetRequriedColumnIndex(this IXLRow row, string header, string path)
        {
            var index = row.GetCellColumnIndex(cell =>
                header.Equals(cell.GetString(), StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                throw new LoadFromExcelException(
                    new LoadFromExcelException.ExceptionKind.ParseError.ColumnHeaderNotFound(
                        header,
                        path,
                        row.Worksheet.Name));
            return index;
        }

        public static double GetRequiredDouble(this IXLCell cell, string path) =>
            cell.DataType == XLDataType.Number
                ? cell.GetDouble()
                : throw new LoadFromExcelException(
                    new LoadFromExcelException.ExceptionKind.ParseError.WrongCellType(
                        path,
                        cell.Worksheet.Name,
                        cell.Address.ToString(),
                        cell.GetString(),
                        LoadFromExcelException.ExceptionKind.ParseError.WrongCellType.CellType
                            .Numeric));

        public static bool GetRequiredBoolean(this IXLCell cell, string path) =>
            cell.DataType == XLDataType.Boolean
                ? cell.GetBoolean()
                : throw new LoadFromExcelException(
                    new LoadFromExcelException.ExceptionKind.ParseError.WrongCellType(
                        path,
                        cell.Worksheet.Name,
                        cell.Address.ToString(),
                        cell.GetString(),
                        LoadFromExcelException.ExceptionKind.ParseError.WrongCellType.CellType
                            .Boolean));
        
        public static int GetRequiredInt(this IXLCell cell, string path) =>
            cell.DataType == XLDataType.Number
                ? (int) Math.Round(cell.GetDouble(), MidpointRounding.AwayFromZero)
                : throw new LoadFromExcelException(
                    new LoadFromExcelException.ExceptionKind.ParseError.WrongCellType(
                        path,
                        cell.Worksheet.Name,
                        cell.Address.ToString(),
                        cell.GetString(),
                        LoadFromExcelException.ExceptionKind.ParseError.WrongCellType.CellType
                            .Numeric));

        public static Length GetRequiredLength(
            this IXLCell cell,
            string path,
            LengthUnit defaultUnit)
        {
            if (cell.DataType == XLDataType.Number)
                return new Length(cell.GetDouble(), defaultUnit);
            if (Length.TryParse(cell.GetString(), out var length))
                return length;
            if (double.TryParse(Regex.Replace(
                        cell.GetString()
                        .Replace(',', '.'), @"\s+", ""), 
                NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
                return new Length(number, defaultUnit);
            throw new LoadFromExcelException(
                new LoadFromExcelException.ExceptionKind.ParseError.WrongCellType(
                    path,
                    cell.Worksheet.Name,
                    cell.Address.ToString(),
                    cell.GetString(),
                    LoadFromExcelException.ExceptionKind.ParseError.WrongCellType.CellType.Length));
        }

        public static Length GetOptionalLength(
            this IXLCell cell,
            string path,
            LengthUnit defaultUnit,
            Length defaultValue = default)
        {
            if (cell.DataType == XLDataType.Number)
                return new Length(cell.GetDouble(), defaultUnit);
            if (Length.TryParse(cell.GetString(), out var length))
                return length;
            if (double.TryParse(Regex.Replace(
                    cell.GetString()
                        .Replace(',', '.'), @"\s+", ""),
                NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
                return new Length(number, defaultUnit);
            return defaultValue;
        }

        public static DecorationColumnMode GetRequiredColumnMode(
            this IXLCell cell,
            string path) =>
            cell.GetString().ToUpper() switch
            {
                "СКРЫТЬ" => DecorationColumnMode.None,
                "СЛЕВА" => DecorationColumnMode.Left,
                "СПРАВА" => DecorationColumnMode.Right,
                "СЛЕВА И СПРАВА" => DecorationColumnMode.LeftAndRight,
                _ => throw new LoadFromExcelException(
                    new LoadFromExcelException.ExceptionKind.ParseError.WrongCellType(
                        path,
                        cell.Worksheet.Name,
                        cell.Address.ToString(),
                        cell.GetString(),
                        LoadFromExcelException.ExceptionKind.ParseError.WrongCellType.CellType
                            .ColumnMode))
            };

        public static Encoding GetRequiredEncoding(
            this IXLCell cell,
            string path)
        {
            try
            {
                return Encoding.GetEncoding(cell.GetString());
            }
            catch
            {
                throw new LoadFromExcelException(
                    new LoadFromExcelException.ExceptionKind.ParseError.WrongCellType(
                        path,
                        cell.Worksheet.Name,
                        cell.Address.ToString(),
                        cell.GetString(),
                        LoadFromExcelException.ExceptionKind.ParseError.WrongCellType.CellType
                            .Encoding));
            }
        }

        public static void SetHeaderValue(
            this IXLCell cell,
            string value)
        {
            cell.SetValue(value);
            cell.Style.Font.Bold = true;
        }

        public static void SetCustomValue(
            this IXLCell cell,
            Length length) =>
            cell.SetValue(length.ToString());

        public static void SetCustomValue(
            this IXLCell cell,
            DecorationColumnMode mode)
        {
            cell.SetValue(mode switch
            {
                DecorationColumnMode.None => "Скрыть",
                DecorationColumnMode.Left => "Слева",
                DecorationColumnMode.Right => "Справа",
                DecorationColumnMode.LeftAndRight => "Слева и справа",
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            });
        }

        public static void SetCustomValue(
            this IXLCell cell,
            Encoding encoding) =>
            cell.SetValue(encoding.WebName);
    }
}