namespace Unity.DebugDisplay
{
    /// <summary>
    /// Color utility for physics debug display.
    /// </summary>
    public struct ColorIndex
    {
        internal int value;

        internal const int kMaxColors = 256;
        internal const int staticColorCount = 248;
        internal const int dynamicColorCount = kMaxColors - staticColorCount;

        const int dynamicColorIndex = staticColorCount;

        /// <summary>
        /// Black.
        /// </summary>
        public static readonly ColorIndex Black = new ColorIndex{value = 0};

        /// <summary>
        /// Red.
        /// </summary>
        public static readonly ColorIndex Red = new ColorIndex{value = 1};

        /// <summary>
        /// Green.
        /// </summary>
        public static readonly ColorIndex Green = new ColorIndex{value = 2};

        /// <summary>
        /// Yellow.
        /// </summary>
        public static readonly ColorIndex Yellow = new ColorIndex{value = 3};

        /// <summary>
        /// Blue.
        /// </summary>
        public static readonly ColorIndex Blue = new ColorIndex{value = 4};

        /// <summary>
        /// Magenta.
        /// </summary>
        public static readonly ColorIndex Magenta = new ColorIndex{value = 5};

        /// <summary>
        /// Cyan.
        /// </summary>
        public static readonly ColorIndex Cyan = new ColorIndex{value = 6};

        /// <summary>
        /// White.
        /// </summary>
        public static readonly ColorIndex White = new ColorIndex{value = 7}; //light grey

        /// <summary>
        /// BrightBlack.
        /// </summary>
        public static readonly ColorIndex BrightBlack = new ColorIndex{value = 8}; //dark grey

        /// <summary>
        /// BrightRed.
        /// </summary>
        public static readonly ColorIndex BrightRed = new ColorIndex{value = 9};

        /// <summary>
        /// BrightGreen.
        /// </summary>
        public static readonly ColorIndex BrightGreen = new ColorIndex{value = 10};

        /// <summary>
        /// BrightYellow.
        /// </summary>
        public static readonly ColorIndex BrightYellow = new ColorIndex{value = 11};

        /// <summary>
        /// BrightBlue.
        /// </summary>
        public static readonly ColorIndex BrightBlue = new ColorIndex{value = 12};

        /// <summary>
        /// BrightMagenta.
        /// </summary>
        public static readonly ColorIndex BrightMagenta = new ColorIndex{value = 13};

        /// <summary>
        /// BrightCyan.
        /// </summary>
        public static readonly ColorIndex BrightCyan = new ColorIndex{value = 14};

        /// <summary>
        /// BrightWhite.
        /// </summary>
        public static readonly ColorIndex BrightWhite = new ColorIndex{value = 15};

        /// <summary>
        /// Grey.
        /// </summary>
        public static readonly ColorIndex Grey12 = new ColorIndex{value = 18}; //0x20, 0x20, 0x20

        /// <summary>
        /// Grey.
        /// </summary>
        public static readonly ColorIndex Grey10 = new ColorIndex{value = 20};

        /// <summary>
        /// Grey.
        /// </summary>
        public static readonly ColorIndex Grey8 = new ColorIndex{value = 22};

        /// <summary>
        /// Grey.
        /// </summary>
        public static readonly ColorIndex Grey6 = new ColorIndex{value = 24};

        /// <summary>
        /// Grey.
        /// </summary>
        public static readonly ColorIndex Grey4 = new ColorIndex{value = 26};

        /// <summary>
        /// StaticGrey.
        /// </summary>
        public static readonly ColorIndex StaticGrey = new ColorIndex{value = 28}; //0xb6, 0xb6, 0xb6,

        /// <summary>
        /// DynamicOrange.
        /// </summary>
        public static readonly ColorIndex DynamicOrange = new ColorIndex{value = 53}; //(0x00, 0xbe, 0xff)

        /// <summary>
        /// Orange.
        /// </summary>
        public static readonly ColorIndex Orange = new ColorIndex{value = 54}; //(0x00, 0x7d, 0xff)

        /// <summary>
        /// OrangeRed.
        /// </summary>
        public static readonly ColorIndex OrangeRed = new ColorIndex{value = 55}; //(0x00, 0x41, 0xff)

        /// Max value is staticColorCount - 1 {247} at the moment.

        /// Next values can change at runtime.

        /// <summary>
        /// DynamicMesh - Dynamic Color.
        /// </summary>
        public static readonly ColorIndex DynamicMesh = new ColorIndex{value = dynamicColorIndex};

        /// <summary>
        /// StaticMesh - Dynamic Color.
        /// </summary>
        public static readonly ColorIndex StaticMesh = new ColorIndex{value = dynamicColorIndex + 1};

        /// <summary>
        /// KinematicMesh - Dynamic Color.
        /// </summary>
        public static readonly ColorIndex KinematicMesh = new ColorIndex{value = dynamicColorIndex + 2};

        internal static ColorIndex Foreground(int value)
        {
            return new ColorIndex {value = (value >> 0) & 0xf};
        }

        internal static ColorIndex Background(int value)
        {
            return new ColorIndex {value = (value >> 4) & 0x7};
        }
    }
}
