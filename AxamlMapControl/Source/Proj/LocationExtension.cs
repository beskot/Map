namespace AxamlMapControl.Source.Proj
{
    /// <summary>
    /// Location extension.
    /// </summary>
    public static class LocationExtension
    {
        /// <summary>
        /// Get dms format of the latitude.
        /// </summary>
        /// <param name="location">Location</param>
        /// <returns>Latitude in dms format</returns>
        public static PointDms GetLatitudeDms(this Location location)
        {
            return PointDms.CreatePointDms(location.Latitude, PointDmsType.Latitude);
        }

        /// <summary>
        /// Get dms format of the longitude.
        /// </summary>
        /// <param name="location">Location</param>
        /// <returns>Longitude in dms format</returns>
        public static PointDms GetLongitudeDms(this Location location)
        {
            return PointDms.CreatePointDms(Location.NormalizeLongitude(location.Longitude), PointDmsType.Longitude);
        }
    }
}