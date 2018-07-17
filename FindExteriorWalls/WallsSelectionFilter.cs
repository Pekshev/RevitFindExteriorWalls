using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace FindExteriorWalls
{
    public class WallsSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Wall;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            throw new NotImplementedException();
        }
    }
}
