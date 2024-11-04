using System.Runtime.InteropServices;

namespace Viewer;

public class WaterSteamEquationOfStateWrapper : IDisposable
{
    private IntPtr _instance;

    private delegate int CalculationDelegate(
        IntPtr instance,
        [In] double[] temperatures,
        [In] double[] pressures,
        [Out] double[] results,
        UIntPtr length);

    [DllImport("WaterSteamEquationOfState.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr CreateWaterSteamEquationOfState(string databasePath);

    [DllImport("WaterSteamEquationOfState.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void DestroyWaterSteamEquationOfState(IntPtr instance);

    [DllImport("WaterSteamEquationOfState.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int CalculateSpecificEnthalpyArray(IntPtr instance, double[] temperatures, double[] pressures, double[] enthalpies, UIntPtr length);

    [DllImport("WaterSteamEquationOfState.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int CalculateSpecificEntropyArray(IntPtr instance, double[] temperatures, double[] pressures, double[] entropies, UIntPtr length);

    [DllImport("WaterSteamEquationOfState.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int CalculateSpecificVolumeArray(IntPtr instance, double[] temperatures, double[] pressures, double[] specificVolumes, UIntPtr length);

    [DllImport("WaterSteamEquationOfState.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int CalculateSpecificInternalEnergyArray(IntPtr instance, double[] temperatures, double[] pressures, double[] specificInternalEnergies, UIntPtr length);

    [DllImport("WaterSteamEquationOfState.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int CalculateSpecificIsobaricHeatCapacityArray(IntPtr instance, double[] temperatures, double[] pressures, double[] specificHeatCapacities, UIntPtr length);

    [DllImport("WaterSteamEquationOfState.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int CalculateSpecificIsochoricHeatCapacityArray(IntPtr instance, double[] temperatures, double[] pressures, double[] specificHeatCapacities, UIntPtr length);

    [DllImport("WaterSteamEquationOfState.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int CalculateSpeedOfSoundArray(IntPtr instance, double[] temperatures, double[] pressures, double[] speedsOfSound, UIntPtr length);

    [DllImport("WaterSteamEquationOfState.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int CalculateDensityArray(IntPtr instance, double[] temperatures, double[] pressures, double[] densities, UIntPtr length);

    public WaterSteamEquationOfStateWrapper(string databasePath)
    {
        _instance = CreateWaterSteamEquationOfState(databasePath);
        if (_instance == IntPtr.Zero)
        {
            throw new InitializationException("Failed to create WaterSteamEquationOfState instance.");
        }
    }

    ~WaterSteamEquationOfStateWrapper()
    {
        Dispose();
    }

    private double[] CalculateArray(double[] temperatures, double[] pressures, CalculationDelegate calculationMethod, string errorMessage)
    {
        if (temperatures.Length != pressures.Length)
        {
            throw new ArgumentException("Temperatures and pressures must have the same number of elements.");
        }

        var results = new double[temperatures.Length];
        ExecuteWithExceptionHandling(() =>
                calculationMethod(_instance, temperatures, pressures, results, (UIntPtr)temperatures.Length),
            errorMessage
        );

        return results;
    }
    
    public double[] CalculateSpecificEnthalpyArray(double[] temperatures, double[] pressures)
        => CalculateArray(temperatures, pressures, CalculateSpecificEnthalpyArray, "Error calculating specific enthalpy.");

    public double[] CalculateSpecificEntropyArray(double[] temperatures, double[] pressures)
        => CalculateArray(temperatures, pressures, CalculateSpecificEntropyArray, "Error calculating specific entropy.");

    public double[] CalculateSpecificVolumeArray(double[] temperatures, double[] pressures)
        => CalculateArray(temperatures, pressures, CalculateSpecificVolumeArray, "Error calculating specific volume.");

    public double[] CalculateSpecificInternalEnergyArray(double[] temperatures, double[] pressures)
        => CalculateArray(temperatures, pressures, CalculateSpecificInternalEnergyArray, "Error calculating specific internal energy.");

    public double[] CalculateSpecificIsobaricHeatCapacityArray(double[] temperatures, double[] pressures)
        => CalculateArray(temperatures, pressures, CalculateSpecificIsobaricHeatCapacityArray, "Error calculating specific isobaric heat capacity.");

    public double[] CalculateSpecificIsochoricHeatCapacityArray(double[] temperatures, double[] pressures)
        => CalculateArray(temperatures, pressures, CalculateSpecificIsochoricHeatCapacityArray, "Error calculating specific isochoric heat capacity.");

    public double[] CalculateSpeedOfSoundArray(double[] temperatures, double[] pressures)
        => CalculateArray(temperatures, pressures, CalculateSpeedOfSoundArray, "Error calculating speed of sound.");

    public double[] CalculateDensityArray(double[] temperatures, double[] pressures)
        => CalculateArray(temperatures, pressures, CalculateDensityArray, "Error calculating density.");

    private static void ExecuteWithExceptionHandling(Func<int> func, string errorMessage)
    {
        var result = func();
        if (result != 0)
        {
            throw new CalculationException(errorMessage);
        }
    }

    public void Dispose()
    {
        if (_instance != IntPtr.Zero)
        {
            DestroyWaterSteamEquationOfState(_instance);
            _instance = IntPtr.Zero;
        }
        GC.SuppressFinalize(this);
    }
}

public class InitializationException(string message) : Exception(message);

public class CalculationException(string message) : Exception(message);