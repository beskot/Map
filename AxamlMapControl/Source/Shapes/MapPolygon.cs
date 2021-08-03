namespace AxamlMapControl.Source.Shapes
{
    /// <summary>
    /// Map shape of polygon.
    /// </summary>
    public class MapPolygon : MapPolyline
    {
        /// <summary>
        /// Update
        /// </summary>
        protected override void Update()
        {
            Update(true, true);
        }
    }
}