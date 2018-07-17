using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace FindExteriorWalls
{
    public static class Extensions
    {
        /// <summary>Get perpendicular to wall's location curve</summary>
        /// <param name="wall">Wall</param>
        /// <param name="leftRight">0 - left, 1 - right</param>
        /// <returns>Long line</returns>
        public static Line GetPerpendicularLine(this Wall wall, int leftRight)
        {
            var wallCurve = ((LocationCurve)wall.Location).Curve;
            XYZ orientation = wall.Orientation;
            if (wallCurve is Arc arc)
                orientation = ((arc.GetEndPoint(0) + arc.GetEndPoint(1)) / 2 - arc.GetCenterPoint()).Normalize();
            return Line.CreateBound(wallCurve.GetCenterPoint(),
                leftRight == 0
                    ? wallCurve.GetCenterPoint() - orientation * 1000
                    : wallCurve.GetCenterPoint() + orientation * 1000);
        }

        public static Line GetPerpendicularLine(this Curve curve, Wall wall, int leftRight)
        {
            XYZ orientation = wall.Orientation;
            if (curve is Arc arc)
                orientation = ((arc.GetEndPoint(0) + arc.GetEndPoint(1)) / 2 - arc.GetCenterPoint()).Normalize();
            return Line.CreateBound(curve.GetCenterPoint(),
                leftRight == 0
                    ? curve.GetCenterPoint() - orientation * 1000
                    : curve.GetCenterPoint() + orientation * 1000);
        }

        public static XYZ GetCenterPoint(this Curve curve)
        {
            if (curve is Arc arc)
            {
                return arc.Evaluate(0.5, true);
            }

            return (curve.GetEndPoint(0) + curve.GetEndPoint(1)) / 2;
        }

        /// <summary>Check intersection with curve by moving z</summary>
        /// <param name="line">Current line</param>
        /// <param name="checkedCurve">Checked curve</param>
        public static bool IntersectToByMovingZ(this Line line, Curve checkedCurve)
        {
            // walls is always vertical - it's very good =)
            var z = line.GetCenterPoint().Z;
            checkedCurve = GetCurveWithChangedZ(checkedCurve, z);
            if (checkedCurve == null) return false; // can't be...
            return line.Intersect(checkedCurve) == SetComparisonResult.Overlap;
        }

        private static Curve GetCurveWithChangedZ(Curve curve, double z)
        {
            if (curve is Line line)
                return Line.CreateBound(
                    new XYZ(line.GetEndPoint(0).X, line.GetEndPoint(0).Y, z),
                    new XYZ(line.GetEndPoint(1).X, line.GetEndPoint(1).Y, z));
            if (curve is Arc arc)
                return Arc.Create(
                    new XYZ(arc.GetEndPoint(0).X, arc.GetEndPoint(0).Y, z),
                    new XYZ(arc.GetEndPoint(1).X, arc.GetEndPoint(1).Y, z),
                    new XYZ(arc.GetCenterPoint().X, arc.GetCenterPoint().Y, z));
            return null;
        }

        public static bool HasWallById(this List<Wall> listOfWalls, Wall checkedWall)
        {
            return listOfWalls.Select(w => w.Id.IntegerValue).Contains(checkedWall.Id.IntegerValue);
        }
    }
}
