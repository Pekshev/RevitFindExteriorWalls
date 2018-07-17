﻿using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace FindExteriorWalls
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class FindExteriorWallsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument.Document;
            var selection = commandData.Application.ActiveUIDocument.Selection;
            try
            {
                // check view
                if (doc.ActiveView.ViewType != ViewType.FloorPlan)
                {
                    TaskDialog.Show("Wrong view", "Must be floor plan");
                    return Result.Cancelled;
                }
                // select walls
                List<Wall> selectedWalls;
                try
                {
                    selectedWalls = selection.PickElementsByRectangle(new WallsSelectionFilter(), "Select walls").Cast<Wall>().ToList();
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }

                if (!selectedWalls.Any()) return Result.Cancelled;

                // result list
                List<Wall> exteriorWalls = new List<Wall>();

                // step one - find by ray
                for (var i = 0; i < selectedWalls.Count; i++)
                {
                    if (selectedWalls[i] == null) continue;
                    var wall = selectedWalls[i];
                    var wallCurve = ((LocationCurve)wall.Location).Curve;
                    bool isExterior = true;
                    // split wall curve for 3 part for greater accuracy
                    for (int k = 1; k <= 3; k++)
                    {
                        Curve tempCurve = Line.CreateBound(wallCurve.GetEndPoint(0), wallCurve.GetCenterPoint());
                        if (k == 2)
                            tempCurve = wallCurve;
                        if(k == 3)
                            tempCurve = Line.CreateBound(wallCurve.GetCenterPoint(), wallCurve.GetEndPoint(1));
                        int intesectionsOnLeft = 0;
                        int intersectionOnRight = 0;
                        Line leftLine = tempCurve.GetPerpendicularLine(wall, 0);
                        Line rightLine = tempCurve.GetPerpendicularLine(wall, 1);
                        for (var j = 0; j < selectedWalls.Count; j++)
                        {
                            if (selectedWalls[j] == null || i == j) continue;
                            var checkedWall = selectedWalls[j];
                            var checkedWallCurve = ((LocationCurve) checkedWall.Location).Curve;
                            // pass the co-directional straight walls
                            if (wallCurve is Line line1 && checkedWallCurve is Line line2 &&
                                Math.Abs(Math.Abs(line1.Direction.DotProduct(line2.Direction))) < 0.0001)
                                continue;
                            if (leftLine.IntersectToByMovingZ(checkedWallCurve))
                                intesectionsOnLeft++;
                            if (rightLine.IntersectToByMovingZ(checkedWallCurve))
                                intersectionOnRight++;
                        }

                        if (!(intesectionsOnLeft == 0 & intersectionOnRight != 0) && !(intersectionOnRight == 0 & intesectionsOnLeft != 0))
                        {
                            isExterior = false;
                            break;
                        }
                    }

                    if (isExterior)
                        if (!exteriorWalls.Contains(wall))
                            exteriorWalls.Add(wall);
                }
                // step two - find by end intersections
                bool hasIntersections = true;
                int overflow = 0;
                while (hasIntersections)
                {
                    hasIntersections = false;
                    foreach (var selectedWall in selectedWalls)
                    {
                        if (exteriorWalls.Contains(selectedWall)) continue;
                        var intersectedByEndsWalls = GetIntersectedByEndsWalls(selectedWalls, exteriorWalls, selectedWall);
                        var allInExterior = true;
                        foreach (var wall in intersectedByEndsWalls)
                        {
                            if (!exteriorWalls.HasWallById(wall))
                            {
                                allInExterior = false;
                                break;
                            }
                        }

                        if (allInExterior)
                        {
                            exteriorWalls.Add(selectedWall);
                            hasIntersections = true;
                        }
                    }

                    overflow++;
                    if (overflow == 1000)
                    {
                        TaskDialog.Show("Error", "Overflow error");
                        break;
                    }
                }

                // show exterior walls
                selection.SetElementIds(exteriorWalls.Select(w => w.Id).ToList());

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message += exception.Message;
                return Result.Failed;
            }
        }

        private List<Wall> GetIntersectedByEndsWalls(List<Wall> selectedWalls, List<Wall> exteriorWalls, Wall currentWall)
        {
            List<Wall> intersectedWalls = new List<Wall>();
            // find by location curve
            var intersectedWithFirstEnd = GetWallsIntersectedWithCurveByEnd(exteriorWalls, currentWall, 0);
            var intersectedWithSecondEnd = GetWallsIntersectedWithCurveByEnd(exteriorWalls, currentWall, 1);
            // find by ends
            var locCurve = (LocationCurve)currentWall.Location;
            var elementsAtEnd = locCurve.get_ElementsAtJoin(0);
            foreach (Element e in elementsAtEnd)
            {
                // Если всего одно пересечение и это сама стена - значит это торец
                if (e.Id.IntegerValue == currentWall.Id.IntegerValue) continue;
                if (selectedWalls.HasWallById((Wall)e))
                {
                    intersectedWalls.Add((Wall)e);
                    // если есть в списке пересекаемых по направляющей, то удалим из списка
                    if (intersectedWithFirstEnd.Any(w => w.Id.IntegerValue == e.Id.IntegerValue))
                        intersectedWithFirstEnd.Remove(
                            intersectedWithFirstEnd.First(w => w.Id.IntegerValue == e.Id.IntegerValue));
                }
            }
            elementsAtEnd = locCurve.get_ElementsAtJoin(1);
            foreach (Element e in elementsAtEnd)
            {
                // Если всего одно пересечение и это сама стена - значит это торец
                if (e.Id.IntegerValue == currentWall.Id.IntegerValue) continue;
                if (selectedWalls.HasWallById((Wall)e))
                {
                    intersectedWalls.Add((Wall)e);
                    // если есть в списке пересекаемых по направляющей, то удалим из списка
                    if (intersectedWithSecondEnd.Any(w => w.Id.IntegerValue == e.Id.IntegerValue))
                        intersectedWithSecondEnd.Remove(
                            intersectedWithSecondEnd.First(w => w.Id.IntegerValue == e.Id.IntegerValue));
                }
            }

            foreach (Wall e in intersectedWithFirstEnd)
                intersectedWalls.Add(e);
            foreach (Wall e in intersectedWithSecondEnd)
                intersectedWalls.Add(e);

            return intersectedWalls;
        }

        private static List<Wall> GetWallsIntersectedWithCurveByEnd(List<Wall> exteriorWalls, Wall currentWall, int endIndex)
        {
            List<Wall> intersectedWalls = new List<Wall>();
            var currentCurve = ((LocationCurve)currentWall.Location).Curve;
            foreach (var exteriorWall in exteriorWalls)
            {
                var exteriorWallCurve = ((LocationCurve)exteriorWall.Location).Curve;
                if (exteriorWall.Id.IntegerValue == currentWall.Id.IntegerValue) continue;
                // pass the co-directional straight walls
                if (currentCurve is Line line1 && exteriorWallCurve is Line line2 &&
                    Math.Abs(Math.Abs(line1.Direction.DotProduct(line2.Direction))) < 0.0001)
                    continue;
                if (currentCurve.IntersectToByMovingZ(exteriorWallCurve, out var intersectionResultArray) &&
                    intersectionResultArray.Size == 1)
                {
                    if (Math.Abs(intersectionResultArray.get_Item(0).XYZPoint.DistanceTo(currentCurve.GetEndPoint(endIndex))) < 0.0001)
                        intersectedWalls.Add(exteriorWall);
                }
            }
            return intersectedWalls;
        }
    }
}
