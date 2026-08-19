namespace CarparkAvailability.ApiApp.Services;

// Standard SVY21 to WGS84 conversion (TM projection)
// Reference: https://app.sla.gov.sg/sirent/
public static class Svy21Converter
{
    private const double A = 6378137.0;
    private const double F = 1.0 / 298.257223563;
    private const double OLat = 1.366666;
    private const double OLon = 103.833333;
    private const double No = 38744.572;
    private const double Eo = 28001.642;
    private const double K = 1.0;

    private static readonly double B = A * (1.0 - F);
    private static readonly double E2 = 2.0 * F - F * F;
    private static readonly double E4 = E2 * E2;
    private static readonly double E6 = E4 * E2;
    private static readonly double A0 = 1.0 - (E2 / 4.0) - (3.0 * E4 / 64.0) - (5.0 * E6 / 256.0);
    private static readonly double A2 = (3.0 / 8.0) * (E2 + (E4 / 4.0) + (15.0 * E6 / 128.0));
    private static readonly double A4 = (15.0 / 256.0) * (E4 + (3.0 * E6 / 4.0));
    private static readonly double A6 = 35.0 * E6 / 3072.0;

    public static (double lat, double lng) Convert(double x, double y)
    {
        double northingPrime = y - No;
        double meridionalArcOrigin = CalculateMeridionalArc(OLat);
        double meridionalArcPrime = meridionalArcOrigin + northingPrime / K;

        double n = (A - B) / (A + B);
        double n2 = n * n;
        double n3 = n2 * n;
        double n4 = n2 * n2;
        double G = A * (1.0 - n) * (1.0 - n2) * (1.0 + (9.0 * n2 / 4.0) + (225.0 * n4 / 64.0)) * (Math.PI / 180.0);
        double sigma = (meridionalArcPrime * Math.PI) / (180.0 * G);

        double latPrime = sigma
            + ((3.0 * n / 2.0) - (27.0 * n3 / 32.0)) * Math.Sin(2.0 * sigma)
            + ((21.0 * n2 / 16.0) - (55.0 * n4 / 32.0)) * Math.Sin(4.0 * sigma)
            + (151.0 * n3 / 96.0) * Math.Sin(6.0 * sigma)
            + (1097.0 * n4 / 512.0) * Math.Sin(8.0 * sigma);

        double sinLatPrime = Math.Sin(latPrime);
        double sinLatPrimeSquared = sinLatPrime * sinLatPrime;
        double rhoPrime = CalculateRho(sinLatPrimeSquared);
        double nuPrime = CalculateNu(sinLatPrimeSquared);
        double psiPrime = nuPrime / rhoPrime;
        double psiPrime2 = psiPrime * psiPrime;
        double psiPrime3 = psiPrime2 * psiPrime;
        double psiPrime4 = psiPrime3 * psiPrime;
        double tanLatPrime = Math.Tan(latPrime);
        double tanLatPrime2 = tanLatPrime * tanLatPrime;
        double tanLatPrime4 = tanLatPrime2 * tanLatPrime2;
        double tanLatPrime6 = tanLatPrime4 * tanLatPrime2;
        double eastingPrime = x - Eo;
        double xPrime = eastingPrime / (K * nuPrime);
        double xPrime2 = xPrime * xPrime;
        double xPrime3 = xPrime2 * xPrime;
        double xPrime5 = xPrime3 * xPrime2;
        double xPrime7 = xPrime5 * xPrime2;

        double latFactor = tanLatPrime / (K * rhoPrime);
        double latTerm1 = latFactor * ((eastingPrime * xPrime) / 2.0);
        double latTerm2 = latFactor * ((eastingPrime * xPrime3) / 24.0)
            * ((-4.0 * psiPrime2) + (9.0 * psiPrime * (1.0 - tanLatPrime2)) + (12.0 * tanLatPrime2));
        double latTerm3 = latFactor * ((eastingPrime * xPrime5) / 720.0)
            * ((8.0 * psiPrime4 * (11.0 - 24.0 * tanLatPrime2))
                - (12.0 * psiPrime3 * (21.0 - 71.0 * tanLatPrime2))
                + (15.0 * psiPrime2 * (15.0 - 98.0 * tanLatPrime2 + 15.0 * tanLatPrime4))
                + (180.0 * psiPrime * (5.0 * tanLatPrime2 - 3.0 * tanLatPrime4))
                + 360.0 * tanLatPrime4);
        double latTerm4 = latFactor * ((eastingPrime * xPrime7) / 40320.0)
            * (1385.0 - 3633.0 * tanLatPrime2 + 4095.0 * tanLatPrime4 + 1575.0 * tanLatPrime6);
        double lat = latPrime - latTerm1 + latTerm2 - latTerm3 + latTerm4;

        double secLatPrime = 1.0 / Math.Cos(lat);
        double lonTerm1 = xPrime * secLatPrime;
        double lonTerm2 = ((xPrime3 * secLatPrime) / 6.0) * (psiPrime + 2.0 * tanLatPrime2);
        double lonTerm3 = ((xPrime5 * secLatPrime) / 120.0)
            * ((-4.0 * psiPrime3 * (1.0 - 6.0 * tanLatPrime2))
                + (psiPrime2 * (9.0 - 68.0 * tanLatPrime2))
                + (72.0 * psiPrime * tanLatPrime2)
                + (24.0 * tanLatPrime4));
        double lonTerm4 = ((xPrime7 * secLatPrime) / 5040.0)
            * (61.0 + 662.0 * tanLatPrime2 + 1320.0 * tanLatPrime4 + 720.0 * tanLatPrime6);
        double lon = (OLon * Math.PI / 180.0) + lonTerm1 - lonTerm2 + lonTerm3 - lonTerm4;

        return (lat * 180.0 / Math.PI, lon * 180.0 / Math.PI);
    }

    private static double CalculateMeridionalArc(double latitudeDegrees)
    {
        double latitude = latitudeDegrees * Math.PI / 180.0;
        return A * ((A0 * latitude) - (A2 * Math.Sin(2.0 * latitude)) + (A4 * Math.Sin(4.0 * latitude)) - (A6 * Math.Sin(6.0 * latitude)));
    }

    private static double CalculateRho(double sinSquaredLatitude)
        => A * (1.0 - E2) / Math.Pow(1.0 - E2 * sinSquaredLatitude, 1.5);

    private static double CalculateNu(double sinSquaredLatitude)
        => A / Math.Sqrt(1.0 - E2 * sinSquaredLatitude);
}
