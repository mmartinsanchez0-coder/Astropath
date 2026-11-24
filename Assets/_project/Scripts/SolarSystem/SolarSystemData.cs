using System;

/// <summary>
/// Datos reales del sistema solar y fórmulas de posición (órbitas elípticas de Kepler).
/// No depende de Unity: solo calcula posiciones en "unidades de Unity".
/// Planetas: elementos orbitales medios de J2000 (JPL, "Approximate Positions of the Planets").
/// Lunas: radios y periodos reales; la fase inicial es real solo para la Luna.
/// </summary>
public static class SolarSystemData
{
    // =====================  ESCALAS (puedes cambiarlas)  =====================
    // 1 km real  = KmToUnits unidades de Unity  (tamaño de los cuerpos y órbitas de las lunas)
    // 1 UA real  = AuToUnits unidades de Unity  (distancia de los planetas al Sol)
    public const double KmToUnits = 0.2;
    public const double AuToUnits = 1000000.0;

    public const double SunDiameterKm = 1392700.0;
    const double Deg2Rad = Math.PI / 180.0;

    public class Planet
    {
        public string Id;
        public double DiameterKm;
        public double A, E, I, L0, LonPeri, LonNode, RatePerCentury;
        public Planet(string id, double diameterKm, double a, double e, double i,
                      double l0, double lonPeri, double lonNode, double ratePerCentury)
        {
            Id = id; DiameterKm = diameterKm; A = a; E = e; I = i;
            L0 = l0; LonPeri = lonPeri; LonNode = lonNode; RatePerCentury = ratePerCentury;
        }
    }

    public class Moon
    {
        public string Id, Parent;
        public double DiameterKm, OrbitKm, PeriodDays, Phase0, TiltDeg;
        public bool Retrograde;
        public Moon(string id, string parent, double diameterKm, double orbitKm,
                    double periodDays, double phase0, double tiltDeg, bool retrograde = false)
        {
            Id = id; Parent = parent; DiameterKm = diameterKm; OrbitKm = orbitKm;
            PeriodDays = periodDays; Phase0 = phase0; TiltDeg = tiltDeg; Retrograde = retrograde;
        }
    }

    //                      id        diámetro km   a (UA)       e           i (°)        L0 (°)        long.perih.   long.nodo     °/siglo
    public static readonly Planet[] Planets =
    {
        new Planet("Mercury",    4879, 0.38709927, 0.20563593,  7.00497902, 252.25032350,   77.45779628,  48.33076593, 149472.67411175),
        new Planet("Venus",     12104, 0.72333566, 0.00677672,  3.39467605, 181.97909950,  131.60246718,  76.67984255,  58517.81538729),
        new Planet("Earth",     12742, 1.00000261, 0.01671123, -0.00001531, 100.46457166,  102.93768193,   0.00000000,  35999.37244981),
        new Planet("Mars",       6779, 1.52371034, 0.09339410,  1.84969142,  -4.55343205,  -23.94362959,  49.55953891,  19140.30268499),
        new Planet("Jupiter",  139820, 5.20288700, 0.04838624,  1.30439695,  34.39644051,   14.72847983, 100.47390909,   3034.74612775),
        new Planet("Saturn",   116460, 9.53667594, 0.05386179,  2.48599187,  49.95424423,   92.59887831, 113.66242448,   1222.49362201),
        new Planet("Uranus",    50724, 19.18916464, 0.04725744, 0.77263783, 313.23810451,  170.95427630,  74.01692503,    428.48202785),
        new Planet("Neptune",   49244, 30.06992276, 0.00859048, 1.77004347, -55.12002969,   44.96476227, 131.78422574,    218.45945325),
        // Planetas enanos. Elementos orbitales APROXIMADOS (la fase orbital, L0, está calculada
        // para que la posición de hoy sea aproximadamente la real; es válido para un juego, no para astronomía).
        new Planet("Ceres", 939, 2.7675, 0.0758, 10.59, 219.7035, 153.9000, 80.3000, 7824.4570),
        new Planet("Eris", 2326, 67.78, 0.4407, 44.04, 17.2202, 187.5000, 35.9500, 64.3928),
        new Planet("Haumea", 1632, 43.13, 0.1951, 28.21, 188.1717, 1.1000, 122.1000, 126.4327),
        new Planet("Makemake", 1430, 45.51, 0.1559, 28.98, 156.4388, 16.0000, 79.3000, 117.6118),
    };

    //                    id          planeta     diámetro  órbita km   periodo d   fase0 (°)  inclinación (°)  retrógrada
    public static readonly Moon[] Moons =
    {
        new Moon("Moon",      "Earth",     3474,   384400,  27.321661, 218.3164,  5.145),
        new Moon("Io",        "Jupiter",   3643,   421700,   1.769138,   0.0,     3.13),
        new Moon("Europa",    "Jupiter",   3122,   671100,   3.551181,  90.0,     3.13),
        new Moon("Ganymede",  "Jupiter",   5268,  1070400,   7.154553, 180.0,     3.13),
        new Moon("Callisto",  "Jupiter",   4821,  1882700,  16.689017, 270.0,     3.13),
        new Moon("Enceladus", "Saturn",     504,   238000,   1.370218,   0.0,    26.73),
        new Moon("Titan",     "Saturn",    5150,  1221900,  15.945421, 120.0,    26.73),
        new Moon("Triton",    "Neptune",   2707,   354800,   5.876854,   0.0,    28.32, true),
    };

    static double SolveKepler(double m, double e)
    {
        double ea = m + e * Math.Sin(m);
        for (int k = 0; k < 8; k++)
            ea -= (ea - e * Math.Sin(ea) - m) / (1.0 - e * Math.Cos(ea));
        return ea;
    }

    /// <summary>Posición del planeta respecto al Sol, en unidades de Unity (Y = arriba = norte de la eclíptica).</summary>
    /// <param name="days">Días transcurridos desde J2000 (1-ene-2000 12:00 UTC).</param>
    public static void PlanetPosition(Planet p, double days, out double ux, out double uy, out double uz)
    {
        double l = p.L0 + p.RatePerCentury * days / 36525.0;           // longitud media (°)
        double m = ((l - p.LonPeri) * Deg2Rad + Math.PI) % (2.0 * Math.PI);
        if (m < 0) m += 2.0 * Math.PI;
        m -= Math.PI;                                                    // anomalía media en [-pi, pi]
        double ea = SolveKepler(m, p.E);                                 // anomalía excéntrica

        double xp = p.A * (Math.Cos(ea) - p.E);                          // en el plano de la órbita
        double yp = p.A * Math.Sqrt(1.0 - p.E * p.E) * Math.Sin(ea);

        double w = (p.LonPeri - p.LonNode) * Deg2Rad;
        double o = p.LonNode * Deg2Rad;
        double i = p.I * Deg2Rad;
        double cw = Math.Cos(w), sw = Math.Sin(w);
        double co = Math.Cos(o), so = Math.Sin(o);
        double ci = Math.Cos(i), si = Math.Sin(i);

        double x = (cw * co - sw * so * ci) * xp + (-sw * co - cw * so * ci) * yp;   // eclíptica (UA)
        double y = (cw * so + sw * co * ci) * xp + (-sw * so + cw * co * ci) * yp;
        double z = (sw * si) * xp + (cw * si) * yp;

        // Eclíptica (x, y, z) -> Unity (X, Y, Z) = (x, z, y)
        ux = x * AuToUnits;
        uy = z * AuToUnits;
        uz = y * AuToUnits;
    }

    /// <summary>Posición de la luna respecto a su planeta, en unidades de Unity.</summary>
    public static void MoonOffset(Moon mo, double days, out double ux, out double uy, out double uz)
    {
        double dir = mo.Retrograde ? -1.0 : 1.0;
        double ang = (mo.Phase0 + dir * 360.0 / mo.PeriodDays * days) * Deg2Rad;
        double r = mo.OrbitKm * KmToUnits;
        double cx = r * Math.Cos(ang);
        double cz = r * Math.Sin(ang);
        double t = mo.TiltDeg * Deg2Rad;
        ux = cx;
        uy = -cz * Math.Sin(t);
        uz = cz * Math.Cos(t);
    }
}
