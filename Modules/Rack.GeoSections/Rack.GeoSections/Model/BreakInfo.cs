using UnitsNet;

namespace Rack.GeoSections.Model
{
    public sealed class BreakInfo
    {
        public BreakInfo(Break @break, Length leftDepth, Length rightDepth)
        {
            Break = @break;
            LeftDepth = leftDepth;
            RightDepth = rightDepth;
            Depths = $"Слева: {LeftDepth:F2}, справа: {RightDepth:F2}.";
        }

        /// <summary>
        /// Разбивка.
        /// </summary>
        public Break Break { get; }
        
        /// <summary>
        /// Глубина на левом крае разбивки, абс. отм., м.
        /// </summary>
        public Length LeftDepth { get; }

        /// <summary>
        /// Глубина на правом крае разбивки, абс. отм., м.
        /// </summary>
        public Length RightDepth { get; }

        public string Depths { get; } 
    }
}