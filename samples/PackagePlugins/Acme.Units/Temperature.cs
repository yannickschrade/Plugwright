namespace Acme.Units;

public static class Temperature
{
    public static double ToFahrenheit(double celsius) => (celsius * 9 / 5) + 32;
}
