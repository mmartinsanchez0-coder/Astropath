using System.Collections.Generic;

/// <summary>
/// Textos de las misiones: nombre, tipo, 10 pistas y 10 datos curiosos por cuerpo.
/// Las pistas continúan la frase "Aterriza en el planeta que..." (van en minúscula, sin punto).
/// Los datos curiosos se muestran al llegar a un cuerpo.
/// </summary>
public static class MissionContent
{
    class Entry
    {
        public string Name;     // con artículo si lo lleva: "la Tierra", "Marte"
        public string Kind;     // "el planeta", "el satélite", "el planeta enano"
        public string[] Hints;
        public string[] Facts;
    }

    static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();
    static readonly System.Random Rng = new System.Random();

    static readonly string[] SunFacts =
    {
        "El Sol contiene el 99,86 % de la masa de todo el sistema solar.",
        "La luz del Sol tarda unos 8 minutos y 20 segundos en llegar a la Tierra.",
        "En su núcleo la temperatura supera los 15 millones de grados."
    };

    static readonly HashSet<string> MainMoons = new HashSet<string>
        { "Moon", "Io", "Europa", "Ganymede", "Callisto", "Titan", "Triton" };

    /// <summary>1 = planetas, 2 = planetas + satélites principales, 3 = cualquier cuerpo.</summary>
    public static int LevelOf(string id)
    {
        Entry e;
        if (!Entries.TryGetValue(id, out e)) return 3;
        if (e.Kind == "el planeta") return 1;
        if (e.Kind == "el satélite" && MainMoons.Contains(id)) return 2;
        return 3;
    }

    /// <summary>Nombre sin artículo ("Tierra", "Luna") para etiquetas.</summary>
    public static string GetShortName(string id)
    {
        string n = GetName(id);
        return n.StartsWith("la ") ? n.Substring(3) : n;
    }

    public static string GetName(string id)
    {
        Entry e; return Entries.TryGetValue(id, out e) ? e.Name : id;
    }

    public static string GetKindPhrase(string id)
    {
        Entry e; return Entries.TryGetValue(id, out e) ? e.Kind : "el cuerpo";
    }

    public static bool IsDwarfPlanet(string id)
    {
        Entry e; return Entries.TryGetValue(id, out e) && e.Kind == "el planeta enano";
    }

    public static bool HasHints(string id)
    {
        Entry e; return Entries.TryGetValue(id, out e) && e.Hints != null && e.Hints.Length > 0;
    }

    /// <summary>Devuelve 'count' pistas distintas elegidas al azar.</summary>
    public static string[] PickHints(string id, int count)
    {
        Entry e;
        if (!Entries.TryGetValue(id, out e) || e.Hints == null) return new string[0];
        List<string> pool = new List<string>(e.Hints);
        List<string> picked = new List<string>();
        while (picked.Count < count && pool.Count > 0)
        {
            int i = Rng.Next(pool.Count);
            picked.Add(pool[i]);
            pool.RemoveAt(i);
        }
        return picked.ToArray();
    }

    public static string RandomFact(string id)
    {
        Entry e;
        if (!Entries.TryGetValue(id, out e) || e.Facts == null || e.Facts.Length == 0)
            return "Has completado el viaje hasta tu destino.";
        return e.Facts[Rng.Next(e.Facts.Length)];
    }

    public static string RandomSunFact()
    {
        return SunFacts[Rng.Next(SunFacts.Length)];
    }

    static void Add(string id, string name, string kind, string[] hints, string[] facts)
    {
        Entries[id] = new Entry { Name = name, Kind = kind, Hints = hints, Facts = facts };
    }

    static MissionContent()
    {
        const string P = "el planeta", S = "el satélite", D = "el planeta enano";

        Add("Mercury", "Mercurio", P,
            new[] {
                "es el planeta más cercano al Sol",
                "es el más pequeño de todos los planetas del sistema solar",
                "no tiene lunas ni una atmósfera significativa",
                "completa su órbita alrededor del Sol en solo 88 días terrestres",
                "sufre cambios de temperatura enormes entre el día y la noche",
                "tarda unos 176 días terrestres en pasar de un amanecer al siguiente",
                "esconde hielo de agua en cráteres de sus polos que nunca reciben luz",
                "tiene una superficie llena de cráteres, muy parecida a la de la Luna",
                "lleva el nombre del mensajero de los dioses romanos",
                "tiene un gigantesco núcleo de hierro que ocupa gran parte de su interior" },
            new[] {
                "Un año mercuriano dura solo 88 días terrestres: es el año más corto de todos los planetas.",
                "Aunque es el más cercano al Sol, no es el más caliente: ese título es de Venus.",
                "Sus temperaturas van de unos 430 °C de día a unos -180 °C de noche.",
                "Su diámetro, de unos 4.880 km, es solo un 40 % mayor que el de la Luna.",
                "Entre un amanecer y el siguiente pasan unos 176 días terrestres.",
                "No tiene una atmósfera real, solo una exosfera extremadamente tenue.",
                "La sonda MESSENGER fue la primera en orbitarlo, en 2011.",
                "Su núcleo de hierro ocupa cerca del 85 % de su radio.",
                "En sus polos hay hielo de agua dentro de cráteres que nunca ven el Sol.",
                "La cuenca Caloris, de unos 1.550 km de ancho, es uno de los mayores impactos del sistema solar." });

        Add("Venus", "Venus", P,
            new[] {
                "es el planeta más caliente del sistema solar",
                "gira en sentido contrario al de la mayoría de los planetas",
                "tiene un día más largo que su año",
                "está cubierto por nubes de ácido sulfúrico",
                "tiene una atmósfera formada casi toda por dióxido de carbono",
                "se ve desde la Tierra como el brillante «lucero» del amanecer y del atardecer",
                "tiene casi el mismo tamaño que la Tierra",
                "soporta en su superficie una presión similar a la de 900 metros bajo el mar",
                "no tiene ninguna luna",
                "es el único planeta clásico con nombre de diosa, la del amor" },
            new[] {
                "Su superficie ronda los 465 °C, suficiente para fundir el plomo.",
                "Un día venusiano (243 días terrestres) dura más que su año (225 días).",
                "Gira al revés que la Tierra: allí el Sol saldría por el oeste.",
                "La presión en su superficie es unas 90 veces la de la Tierra.",
                "La sonda soviética Venera 7 fue la primera en transmitir desde su superficie, en 1970.",
                "Sus nubes reflejan tanta luz que es el astro más brillante del cielo nocturno después de la Luna.",
                "Tiene miles de volcanes y es posible que algunos sigan activos.",
                "Su diámetro es el 95 % del terrestre, por eso se le llama el «planeta gemelo».",
                "No tiene lunas ni anillos.",
                "Es el único planeta clásico que lleva nombre de diosa." });

        Add("Earth", "la Tierra", P,
            new string[0],
            new[] {
                "Es el único mundo conocido en el que hay vida.",
                "Cerca del 71 % de su superficie está cubierta por agua.",
                "Su atmósfera es aproximadamente un 78 % nitrógeno y un 21 % oxígeno.",
                "Tarda 365,25 días en orbitar el Sol, por eso existen los años bisiestos.",
                "La Luna estabiliza la inclinación de su eje y provoca las mareas.",
                "Su campo magnético desvía el viento solar y origina las auroras.",
                "Es el planeta más denso del sistema solar.",
                "Su eje, inclinado unos 23,4°, produce las estaciones.",
                "En el ecuador gira a unos 1.670 km/h.",
                "Tiene unos 4.540 millones de años." });

        Add("Moon", "la Luna", S,
            new[] {
                "es el único satélite natural de la Tierra",
                "siempre nos muestra la misma cara",
                "provoca las mareas de nuestros océanos",
                "fue pisado por seres humanos por primera vez en 1969",
                "probablemente se formó tras el choque de un cuerpo gigante contra la Tierra",
                "está cubierto de cráteres y de «mares» de lava solidificada",
                "tiene una gravedad de aproximadamente una sexta parte de la terrestre",
                "se aleja de la Tierra unos 3,8 centímetros cada año",
                "tarda unos 27 días en dar una vuelta completa a la Tierra",
                "es el quinto satélite más grande del sistema solar" },
            new[] {
                "Se aleja de la Tierra unos 3,8 cm cada año.",
                "Doce astronautas caminaron sobre su superficie entre 1969 y 1972.",
                "Su gravedad es el 16,5 % de la terrestre: allí pesarías unas seis veces menos.",
                "Siempre vemos la misma cara porque tarda lo mismo en rotar que en orbitar la Tierra.",
                "Tiene unos 4.500 millones de años y probablemente nació de un impacto gigante.",
                "Sus temperaturas oscilan entre unos 127 °C de día y -173 °C de noche.",
                "Hay hielo de agua en cráteres polares que permanecen siempre en sombra.",
                "Es el quinto satélite más grande del sistema solar.",
                "Su diámetro es algo más de una cuarta parte del de la Tierra.",
                "Los eclipses solares totales son posibles porque, desde la Tierra, la Luna parece casi del mismo tamaño que el Sol." });

        Add("Mars", "Marte", P,
            new[] {
                "se conoce como el planeta rojo",
                "debe su color al óxido de hierro de su superficie",
                "alberga el Monte Olimpo, el volcán más grande del sistema solar",
                "tiene dos pequeñas lunas llamadas Fobos y Deimos",
                "posee un cañón, Valles Marineris, de más de 4.000 km de longitud",
                "tiene casquetes polares de hielo de agua y de dióxido de carbono",
                "tiene un día casi igual de largo que el de la Tierra",
                "lleva el nombre del dios romano de la guerra",
                "es explorado por róveres como Curiosity y Perseverance",
                "tiene una atmósfera muy fina, formada sobre todo por dióxido de carbono" },
            new[] {
                "El Monte Olimpo mide unos 22 km de altura, casi tres veces el Everest.",
                "Un día marciano (un sol) dura 24 horas y 37 minutos.",
                "Valles Marineris se extiende más de 4.000 km, comparable a la anchura de Estados Unidos.",
                "Fobos y Deimos podrían ser asteroides capturados; Fobos se acerca poco a poco a Marte.",
                "Hace miles de millones de años tuvo ríos y lagos de agua líquida.",
                "Su gravedad es un 38 % de la terrestre.",
                "Las tormentas de polvo pueden llegar a cubrir el planeta entero.",
                "Su año dura 687 días terrestres.",
                "Su atmósfera es un 95 % dióxido de carbono y tiene menos del 1 % de la presión terrestre.",
                "El helicóptero Ingenuity logró en 2021 el primer vuelo propulsado en otro planeta." });

        Add("Jupiter", "Júpiter", P,
            new[] {
                "es el planeta más grande del sistema solar",
                "tiene una Gran Mancha Roja, una tormenta mayor que la Tierra",
                "es un gigante gaseoso formado sobre todo por hidrógeno y helio",
                "tiene más de noventa lunas, entre ellas Ío, Europa, Ganímedes y Calisto",
                "tiene el día más corto de todos los planetas: unas 10 horas",
                "posee un tenue sistema de anillos",
                "tiene el campo magnético más potente de los planetas",
                "lleva el nombre del rey de los dioses romanos",
                "es más masivo que todos los demás planetas juntos",
                "ha sido visitado por sondas como Pioneer, Voyager, Galileo y Juno" },
            new[] {
                "Su masa es unas 2,5 veces la de todos los demás planetas juntos.",
                "En su interior cabrían más de 1.300 Tierras.",
                "La Gran Mancha Roja se observa desde hace más de 150 años y se está encogiendo.",
                "Gira tan rápido que su día dura menos de 10 horas.",
                "Galileo descubrió sus cuatro lunas mayores en 1610.",
                "Sus bandas de nubes son corrientes de viento que soplan en sentidos alternos.",
                "No tiene superficie sólida: bajo las nubes el hidrógeno se vuelve líquido y luego metálico.",
                "Irradia más calor del que recibe del Sol.",
                "Tiene las auroras más intensas del sistema solar.",
                "En 1994 el cometa Shoemaker-Levy 9 se estrelló contra él." });

        Add("Saturn", "Saturno", P,
            new[] {
                "destaca por sus enormes anillos visibles",
                "es el segundo planeta más grande del sistema solar",
                "es menos denso que el agua",
                "tiene una luna, Titán, con una atmósfera densa",
                "tiene anillos formados sobre todo por hielo y fragmentos de roca",
                "tiene un huracán de forma hexagonal en su polo norte",
                "tiene una luna, Encélado, que expulsa chorros de agua helada",
                "lleva el nombre del dios romano de la agricultura",
                "fue orbitado durante trece años por la sonda Cassini",
                "se ve a simple vista desde la Tierra como un punto amarillento" },
            new[] {
                "Es menos denso que el agua: flotaría en un océano lo bastante grande.",
                "Sus anillos miden hasta 280.000 km de ancho, pero en muchos puntos solo unas decenas de metros de grosor.",
                "Cassini llegó en 2004 y terminó su misión en 2017 sumergiéndose en su atmósfera.",
                "En su polo norte hay una tormenta hexagonal de unos 30.000 km de ancho.",
                "Tiene más de 140 lunas conocidas.",
                "Sus vientos pueden superar los 1.800 km/h.",
                "Un año saturniano dura casi 29,5 años terrestres.",
                "Su día dura unas 10 horas y media.",
                "Los anillos podrían ser relativamente jóvenes, de unos cientos de millones de años.",
                "Galileo los vio en 1610 y los confundió con dos lunas a cada lado del planeta." });

        Add("Uranus", "Urano", P,
            new[] {
                "gira prácticamente tumbado sobre su eje",
                "es un gigante de hielo de color azul verdoso",
                "fue el primer planeta descubierto con telescopio, en 1781",
                "fue descubierto por William Herschel",
                "tiene lunas con nombres de personajes de Shakespeare y Pope",
                "tiene anillos estrechos y oscuros",
                "tiene la atmósfera más fría de todos los planetas, aunque no es el más lejano",
                "debe su color al metano de su atmósfera",
                "solo ha sido visitado por la Voyager 2, en 1986",
                "tarda 84 años terrestres en dar una vuelta al Sol" },
            new[] {
                "Su eje está inclinado unos 98°, así que rueda de lado por su órbita.",
                "Herschel lo descubrió en 1781 y quiso llamarlo «Georgium Sidus» en honor al rey Jorge III.",
                "Su atmósfera ha llegado a unos -224 °C, la más fría registrada en un planeta.",
                "Cada polo pasa unos 42 años seguidos a la luz del Sol y otros 42 en oscuridad.",
                "El metano de su atmósfera le da el tono azul verdoso.",
                "Tiene 13 anillos conocidos y unas 28 lunas.",
                "Solo la Voyager 2 lo ha sobrevolado, en enero de 1986.",
                "Su diámetro es unas cuatro veces el de la Tierra.",
                "Su rotación es retrógrada, como la de Venus.",
                "Su día dura unas 17 horas." });

        Add("Neptune", "Neptuno", P,
            new[] {
                "es el planeta más lejano del Sol",
                "tiene los vientos más rápidos del sistema solar",
                "fue localizado gracias a cálculos matemáticos antes de verlo con un telescopio",
                "es un gigante de hielo de un azul intenso",
                "tiene una gran luna, Tritón, que orbita en sentido contrario a su rotación",
                "fue descubierto en 1846",
                "solo ha sido visitado por la Voyager 2, en 1989",
                "tardó unos 165 años en completar su primera órbita desde que se descubrió",
                "tuvo una Gran Mancha Oscura que apareció y desapareció",
                "lleva el nombre del dios romano del mar" },
            new[] {
                "Se descubrió en 1846 gracias a los cálculos de Le Verrier y Adams.",
                "Sus vientos alcanzan unos 2.000 km/h, los más veloces conocidos del sistema solar.",
                "Un año neptuniano dura unos 165 años terrestres.",
                "Completó su primera órbita desde el descubrimiento en 2011.",
                "La Voyager 2 lo sobrevoló en agosto de 1989.",
                "Tiene 16 lunas conocidas; la mayor es Tritón.",
                "Su color azul se debe al metano y a otro compuesto aún no identificado.",
                "Irradia unas 2,6 veces más energía de la que recibe del Sol.",
                "Está a unos 4.500 millones de km del Sol, 30 veces la distancia de la Tierra.",
                "Tiene un sistema de anillos tenues con arcos más brillantes." });

        Add("Io", "Ío", S,
            new[] {
                "es el cuerpo con más actividad volcánica del sistema solar",
                "es una luna de Júpiter",
                "tiene colores amarillos y anaranjados por el azufre",
                "es una de las cuatro lunas galileanas",
                "se calienta por dentro con las mareas que provoca Júpiter",
                "tiene volcanes que lanzan columnas de gas a cientos de kilómetros",
                "es la más cercana a Júpiter de las lunas galileanas",
                "casi no tiene cráteres porque su superficie se renueva constantemente",
                "es algo mayor que nuestra Luna",
                "recibe el nombre de una sacerdotisa amada por Zeus" },
            new[] {
                "Tiene más de 400 volcanes activos.",
                "Sus erupciones pueden lanzar material a más de 300 km de altura.",
                "Las mareas de Júpiter y de otras lunas flexionan su interior y lo calientan.",
                "Su superficie se renueva tan deprisa que apenas conserva cráteres.",
                "Da una vuelta a Júpiter en solo 1,77 días.",
                "Su diámetro, de 3.643 km, es algo mayor que el de la Luna.",
                "Galileo la descubrió en 1610 junto con Europa, Ganímedes y Calisto.",
                "Sus colores se deben al azufre y a sus compuestos.",
                "Mantiene una resonancia orbital 4:2:1 con Europa y Ganímedes.",
                "Tiene una atmósfera finísima de dióxido de azufre." });

        Add("Europa", "Europa", S,
            new[] {
                "oculta un océano de agua líquida bajo su corteza de hielo",
                "es una luna de Júpiter",
                "es uno de los mejores candidatos del sistema solar para albergar vida",
                "tiene una superficie de hielo surcada por grietas rojizas",
                "es la más pequeña de las cuatro lunas galileanas",
                "podría tener más agua líquida que todos los océanos de la Tierra juntos",
                "tiene una de las superficies más lisas del sistema solar",
                "espera la visita de la misión Europa Clipper",
                "podría expulsar chorros de vapor de agua",
                "recibe el nombre de una princesa fenicia raptada por Zeus" },
            new[] {
                "Se estima que su océano subterráneo contiene aproximadamente el doble de agua que los océanos de la Tierra.",
                "Su corteza de hielo tendría entre 15 y 25 km de grosor.",
                "Es la sexta luna más grande del sistema solar.",
                "La misión Europa Clipper se lanzó en octubre de 2024 y llegará en 2030.",
                "Tiene muy pocos cráteres, lo que indica una superficie geológicamente joven.",
                "Las líneas rojizas son grietas con sales y otros compuestos.",
                "El telescopio Hubble observó posibles chorros de vapor de agua.",
                "Da una vuelta a Júpiter cada 3,5 días.",
                "Su diámetro, de 3.122 km, es algo menor que el de la Luna.",
                "Tiene una atmósfera de oxígeno extremadamente tenue." });

        Add("Ganymede", "Ganímedes", S,
            new[] {
                "es el satélite más grande del sistema solar",
                "es mayor que el planeta Mercurio",
                "es la única luna conocida con campo magnético propio",
                "es una luna de Júpiter",
                "podría ocultar un océano de agua salada entre capas de hielo",
                "combina zonas oscuras y antiguas con zonas claras y surcadas",
                "fue sobrevolado por la sonda Juno en 2021",
                "tarda unos 7 días en orbitar su planeta",
                "espera la visita de la misión JUICE de la ESA",
                "lleva el nombre del joven copero de los dioses" },
            new[] {
                "Su diámetro, de 5.268 km, supera al de Mercurio, aunque tiene menos masa.",
                "Es la única luna del sistema solar con campo magnético propio.",
                "Se cree que bajo su superficie hay un océano salado con más agua que todos los de la Tierra.",
                "Tiene auroras alrededor de sus polos.",
                "Mantiene una resonancia orbital con Ío y Europa.",
                "La misión JUICE de la ESA lo orbitará a partir de 2034.",
                "Es el noveno objeto más grande del sistema solar, contando el Sol.",
                "Su superficie mezcla terreno oscuro lleno de cráteres con terreno claro y surcado.",
                "Galileo lo descubrió en 1610.",
                "Se cree que tiene un núcleo de hierro líquido que genera su campo magnético." });

        Add("Callisto", "Calisto", S,
            new[] {
                "es la más alejada de Júpiter de las cuatro lunas galileanas",
                "tiene una de las superficies con más cráteres del sistema solar",
                "tiene una superficie muy antigua, de unos 4.000 millones de años",
                "es el tercer satélite más grande del sistema solar",
                "apenas tiene actividad geológica y está lejos de la radiación de Júpiter",
                "podría ocultar un océano subterráneo",
                "tiene una enorme estructura, Valhalla, con anillos concéntricos",
                "se ha propuesto como lugar para una futura base humana",
                "es una luna de Júpiter",
                "tarda casi 17 días en dar una vuelta a su planeta" },
            new[] {
                "Es la tercera luna más grande del sistema solar, con 4.821 km de diámetro.",
                "Su superficie tiene unos 4.000 millones de años y es de las más craterizadas.",
                "La estructura Valhalla tiene anillos que se extienden unos 3.800 km.",
                "Orbita fuera del cinturón de radiación más intenso de Júpiter.",
                "Su interior es una mezcla poco diferenciada de hielo y roca.",
                "Los datos de la sonda Galileo sugieren un posible océano subterráneo.",
                "Siempre muestra la misma cara a Júpiter.",
                "Tiene una atmósfera finísima de dióxido de carbono.",
                "Se ha propuesto como lugar para una futura base tripulada.",
                "Da una vuelta a Júpiter cada 16,7 días." });

        Add("Enceladus", "Encélado", S,
            new[] {
                "expulsa chorros de agua helada desde su polo sur",
                "es una luna de Saturno",
                "es muy pequeña: mide unos 500 km de diámetro",
                "es uno de los cuerpos más brillantes del sistema solar",
                "oculta un océano de agua salada bajo el hielo",
                "alimenta uno de los anillos de Saturno",
                "fue estudiada por la sonda Cassini, que descubrió sus géiseres",
                "tiene fracturas llamadas «rayas de tigre» en su polo sur",
                "es uno de los lugares más prometedores para buscar vida",
                "recibe el nombre de un gigante de la mitología griega" },
            new[] {
                "Su superficie refleja casi toda la luz que recibe, como nieve recién caída.",
                "Sus géiseres alimentan el anillo E de Saturno.",
                "Cassini detectó en sus chorros agua, sales, moléculas orgánicas e hidrógeno.",
                "Mide solo 504 km de diámetro: cabría dentro de la península ibérica.",
                "Su océano está bajo una corteza de hielo que en el polo sur es muy fina.",
                "William Herschel la descubrió en 1789.",
                "Da una vuelta a Saturno en 1,37 días.",
                "Las «rayas de tigre» son fracturas por las que escapa el vapor de agua.",
                "Combina regiones con cráteres antiguos y otras de hielo joven y liso.",
                "Las mareas de Saturno mantienen caliente su interior." });

        Add("Titan", "Titán", S,
            new[] {
                "es la mayor luna de Saturno",
                "tiene una atmósfera más densa que la de la Tierra",
                "es la única luna del sistema solar con una atmósfera espesa",
                "tiene lagos y mares de metano y etano líquidos",
                "recibió en 2005 la visita de la sonda Huygens, que aterrizó en su superficie",
                "tiene una atmósfera formada sobre todo por nitrógeno",
                "es mayor que el planeta Mercurio",
                "tiene un cielo anaranjado que oculta su superficie a simple vista",
                "recibirá la visita del dron Dragonfly de la NASA",
                "tiene un ciclo de lluvias, pero de metano" },
            new[] {
                "Es la segunda luna más grande del sistema solar, con 5.150 km de diámetro.",
                "La presión en su superficie es 1,5 veces la terrestre.",
                "La sonda Huygens aterrizó el 14 de enero de 2005, el aterrizaje más lejano realizado hasta hoy.",
                "Tiene ríos, lagos y mares de metano y etano líquidos.",
                "Es el único cuerpo, además de la Tierra, con líquidos estables en su superficie.",
                "Su atmósfera es un 95 % nitrógeno.",
                "Bajo su superficie podría haber un océano de agua y amoníaco.",
                "El dron nuclear Dragonfly de la NASA despegará en 2028 y llegará en 2034.",
                "Con tan poca gravedad y tanta atmósfera, en teoría un humano podría volar con alas atadas a los brazos.",
                "Su temperatura en la superficie es de unos -179 °C." });

        Add("Triton", "Tritón", S,
            new[] {
                "es la mayor luna de Neptuno",
                "orbita en sentido contrario al de giro de su planeta",
                "probablemente es un objeto capturado del cinturón de Kuiper",
                "tiene géiseres de nitrógeno",
                "es uno de los lugares más fríos del sistema solar",
                "se acerca poco a poco a su planeta y acabará despedazado",
                "tiene una superficie rosada con textura de piel de melón",
                "fue visitado por la Voyager 2 en 1989",
                "fue descubierto solo 17 días después de su planeta",
                "recibe el nombre de un hijo del dios del mar" },
            new[] {
                "Su órbita retrógrada indica que nació fuera del sistema de Neptuno.",
                "Su temperatura en la superficie es de unos -235 °C.",
                "La Voyager 2 vio géiseres de nitrógeno que lanzaban material a unos 8 km de altura.",
                "William Lassell lo descubrió el 10 de octubre de 1846, 17 días después de Neptuno.",
                "Dentro de unos 3.600 millones de años podría romperse y formar un anillo.",
                "Su diámetro es de 2.707 km.",
                "Es la única luna grande del sistema solar con órbita retrógrada.",
                "Tiene una atmósfera tenue de nitrógeno.",
                "Los casquetes de nitrógeno helado le dan tonos rosados.",
                "Su terreno de «piel de melón» no se conoce en ningún otro cuerpo." });

        Add("Ceres", "Ceres", D,
            new[] {
                "es el único planeta enano del cinturón de asteroides",
                "es el objeto más grande del cinturón de asteroides",
                "fue el primer asteroide descubierto, en 1801",
                "fue descubierto por el astrónomo italiano Giuseppe Piazzi",
                "fue orbitado por la sonda Dawn",
                "tiene brillantes manchas de sales en el cráter Occator",
                "recibe el nombre de la diosa romana de la agricultura",
                "esconde mucha agua helada bajo su superficie",
                "se sitúa entre las órbitas de Marte y Júpiter",
                "mide unos 940 km de diámetro" },
            new[] {
                "Contiene cerca de un tercio de la masa total del cinturón de asteroides.",
                "Piazzi lo descubrió el 1 de enero de 1801.",
                "La sonda Dawn llegó en 2015 y fue la primera en orbitar un planeta enano.",
                "Las manchas brillantes del cráter Occator son depósitos de carbonato de sodio.",
                "Bajo su superficie hay agua helada y posiblemente salmuera líquida.",
                "Se clasificó como planeta enano en 2006.",
                "Tiene un volcán de hielo, Ahuna Mons, de unos 4 km de altura.",
                "Su año dura 4,6 años terrestres.",
                "Su gravedad es solo el 3 % de la terrestre.",
                "De su nombre derivan la palabra «cereal» y el elemento químico cerio." });

        Add("Eris", "Eris", D,
            new[] {
                "es el planeta enano más masivo que se conoce",
                "con su descubrimiento llevó a que Plutón dejara de ser considerado planeta",
                "tiene una luna llamada Disnomia",
                "fue descubierto en 2005",
                "tiene una órbita muy alargada e inclinada",
                "pertenece al disco disperso, mucho más allá de Neptuno",
                "refleja casi toda la luz que recibe del Sol",
                "recibe el nombre de la diosa griega de la discordia",
                "orbita mucho más lejos que Neptuno y apenas recibe luz del Sol",
                "tarda unos 559 años en dar una vuelta al Sol" },
            new[] {
                "Es un 27 % más masivo que Plutón, aunque de diámetro casi igual.",
                "Su descubrimiento llevó a la Unión Astronómica Internacional a definir «planeta enano» en 2006.",
                "Tarda unos 559 años en orbitar el Sol.",
                "Su luna Disnomia lleva el nombre de la hija de la diosa Eris.",
                "Su órbita está inclinada unos 44° respecto al plano de los planetas.",
                "Su superficie de hielo de metano refleja casi toda la luz solar.",
                "Antes de su nombre oficial se la apodó «Xena».",
                "Su distancia al Sol varía entre unas 38 y 98 UA.",
                "Su diámetro, de unos 2.326 km, es ligeramente menor que el de Plutón.",
                "Su temperatura media es de unos -240 °C." });

        Add("Haumea", "Haumea", D,
            new[] {
                "tiene forma de balón de rugby",
                "gira sobre sí mismo en menos de cuatro horas",
                "tiene dos lunas llamadas Hi'iaka y Namaka",
                "tiene un anillo, descubierto en 2017",
                "pertenece al cinturón de Kuiper",
                "recibe el nombre de la diosa hawaiana de la fertilidad y el parto",
                "tiene una superficie cubierta de hielo de agua casi puro",
                "fue descubierto a comienzos de los años 2000",
                "es uno de los objetos grandes que giran más rápido del sistema solar",
                "debe su forma y sus lunas, probablemente, a una colisión antigua" },
            new[] {
                "Gira una vez cada 3,9 horas, la rotación más rápida entre los objetos grandes conocidos.",
                "Su forma de elipsoide alcanza casi 2.300 km en su eje mayor.",
                "Su anillo se descubrió en 2017 observando una ocultación de una estrella.",
                "Sus lunas, Hi'iaka y Namaka, se llaman como dos hijas de la diosa.",
                "Su nombre es el de la diosa hawaiana del parto.",
                "Tarda unos 284 años en orbitar el Sol.",
                "Su superficie es de hielo de agua casi puro.",
                "Existe una familia de fragmentos helados con su misma composición, fruto de una colisión.",
                "Fue reconocido como planeta enano en 2008.",
                "Se encuentra de media a unas 43 UA del Sol." });

        Add("Makemake", "Makemake", D,
            new[] {
                "es uno de los planetas enanos más grandes del cinturón de Kuiper",
                "recibe el nombre del dios creador del pueblo rapa nui de la isla de Pascua",
                "fue descubierto en 2005, cerca de la Pascua",
                "tiene una pequeña luna descubierta en 2016",
                "tiene un color rojizo",
                "tiene una superficie cubierta de hielo de metano",
                "es uno de los objetos más brillantes del cinturón de Kuiper, tras Plutón",
                "tarda unos 306 años en dar una vuelta al Sol",
                "fue reconocido como planeta enano en 2008",
                "fue apodado al principio «Conejo de Pascua»" },
            new[] {
                "Se descubrió el 31 de marzo de 2005, pocos días antes de la Pascua, y se le apodó «Conejo de Pascua».",
                "Su nombre viene del dios creador de la mitología rapa nui.",
                "Su diámetro, de unos 1.430 km, es aproximadamente dos tercios del de Plutón.",
                "Su pequeña luna se descubrió en 2016 con el telescopio Hubble.",
                "Su superficie, de metano, etano y nitrógeno helados, es de color rojizo.",
                "Tarda unos 306 años en orbitar el Sol.",
                "Está a unas 45 UA del Sol.",
                "Su temperatura ronda los -240 °C.",
                "Fue reconocido como planeta enano en 2008.",
                "Su día dura unas 22,8 horas." });
    }
}
