using System.ComponentModel;
using OxyPlot.Series;
using OxyPlot;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Reflection;
using OxyPlot.Legends;
using System.Collections.ObjectModel;
using OxyPlot.Axes;

namespace Viewer;

internal enum ThermodynamicProperty
{
    [Description("Density")]
    Density,
    [Description("Specific Enthalpy")]
    SpecificEnthalpy,
    [Description("Specific Entropy")]
    SpecificEntropy,
    [Description("Specific Volume")]
    SpecificVolume,
    [Description("Specific Internal Energy")]
    SpecificInternalEnergy,
    [Description("Specific Isobaric Heat Capacity")]
    SpecificIsobaricHeatCapacity,
    [Description("Specific Isochoric Heat Capacity")]
    SpecificIsochoricHeatCapacity,
    [Description("Speed of Sound")]
    SpeedOfSound
}

internal enum PressureUnit
{
    [Description("MPa")]
    MPa,
    [Description("kPa")]
    kPa,
    [Description("Pa")]
    Pa
}

internal enum TemperatureUnit
{
    [Description("K")]
    K,
    [Description("°C")]
    C
}

internal enum PlotTypes
{
    [Description("Iso-Bar")]
    IsoBar,
    [Description("Iso-Therm")]
    IsoTherm,
}

public static class EnumHelper
{
    public static string GetEnumDescription(Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        var attribute = (DescriptionAttribute)field?.GetCustomAttribute(typeof(DescriptionAttribute))!;
        return attribute?.Description ?? value.ToString();
    }

    public static IEnumerable<string> GetEnumDescriptions<T>() where T : Enum
    {
        return Enum.GetValues(typeof(T)).Cast<Enum>().Select(GetEnumDescription);
    }
    public static T GetEnumValueFromDescription<T>(string description) where T : Enum
    {
        foreach (var value in Enum.GetValues(typeof(T)))
        {
            if (GetEnumDescription((Enum)value) == description)
            {
                return (T)value;
            }
        }
        throw new ArgumentException("No enum found with the specified description.");
    }
}

public partial class MainWindow
{
    private WaterSteamEquationOfStateWrapper? _eosWrapper;
    private string _databasePath = string.Empty;
    private const string EquationOfStateName = "IAPWS IF97 Water/Steam";
    private readonly Dictionary<ThermodynamicProperty, string> _propertyUnits = new()
    {
        { ThermodynamicProperty.Density, "Density (kg/m³)"},
        { ThermodynamicProperty.SpecificEnthalpy, "Specific Enthalpy (kJ/kg)" },
        { ThermodynamicProperty.SpecificEntropy, "Specific Entropy (kJ/(kg·K))" },
        { ThermodynamicProperty.SpecificVolume, "Specific Volume (m³/kg)" },
        { ThermodynamicProperty.SpecificInternalEnergy, "Specific Internal Energy (kJ/kg)" },
        { ThermodynamicProperty.SpecificIsobaricHeatCapacity, "Specific Heat Capacity Cp (kJ/(kg·K))" },
        { ThermodynamicProperty.SpecificIsochoricHeatCapacity, "Specific Heat Capacity Cv (kJ/(kg·K))" },
        { ThermodynamicProperty.SpeedOfSound, "Speed of Sound (m/s)" }
    };
    private const double MinPressureMPa = 0.01;
    private const double MaxPressureMPa = 100;
    private const double MinTemperatureK = 273.16;
    private const double MaxTemperatureBelow50MPa = 2273.15;
    private const double MaxTemperatureAbove50MPa = 1073.15;

    public ObservableCollection<double> IsoBarValues { get; } = [];
    public ObservableCollection<double> IsoThermValues { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        SetControlsEnabled(false);
        foreach (var description in EnumHelper.GetEnumDescriptions<ThermodynamicProperty>())
        {
            PropertyComboBox.Items.Add(new ComboBoxItem { Content = description });
        }
        foreach (var description in EnumHelper.GetEnumDescriptions<PressureUnit>())
        {
            CalcPressureUnitComboBox.Items.Add(new ComboBoxItem { Content = description });
            PressureUnitComboBox.Items.Add(new ComboBoxItem { Content = description });
        }
        foreach (var description in EnumHelper.GetEnumDescriptions<PlotTypes>())
        {
            PlotTypeComboBox.Items.Add(new ComboBoxItem { Content = description });
        }
        foreach (var description in EnumHelper.GetEnumDescriptions<TemperatureUnit>())
        {
            CalcTemperatureUnitComboBox.Items.Add(new ComboBoxItem { Content = description });
            TemperatureUnitComboBox.Items.Add(new ComboBoxItem { Content = description });
        }
        PressureUnitComboBox.SelectionChanged += PressureUnitComboBox_SelectionChanged;
        TemperatureUnitComboBox.SelectionChanged += TemperatureUnitComboBox_SelectionChanged;
        Loaded += (s, e) =>
        {
            PressureUnitComboBox.SelectedItem = PressureUnitComboBox.Items
                .Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Content.ToString() == EnumHelper.GetEnumDescription(PressureUnit.MPa));

            TemperatureUnitComboBox.SelectedItem = TemperatureUnitComboBox.Items
                .Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Content.ToString() == EnumHelper.GetEnumDescription(TemperatureUnit.K));

            CalcPressureUnitComboBox.SelectedItem = CalcPressureUnitComboBox.Items
                .Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Content.ToString() == EnumHelper.GetEnumDescription(PressureUnit.MPa));

            CalcTemperatureUnitComboBox.SelectedItem = CalcTemperatureUnitComboBox.Items
                .Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Content.ToString() == EnumHelper.GetEnumDescription(TemperatureUnit.K));
        };
    }

    #region IsoProperties I/O

    private void SetControlsEnabled(bool isEnabled)
    {
        PlotTypeComboBox.IsEnabled = isEnabled;
        PropertyComboBox.IsEnabled = isEnabled;
        PlotButton.IsEnabled = isEnabled;

        PressureSlider.IsEnabled = isEnabled;
        PressureTextBox.IsEnabled = isEnabled;
        PressureUnitComboBox.IsEnabled = isEnabled;

        TemperatureSlider.IsEnabled = isEnabled;
        TemperatureTextBox.IsEnabled = isEnabled;
        TemperatureUnitComboBox.IsEnabled = isEnabled;
    }

    private void SetDatabasePath_Click(object sender, RoutedEventArgs e)
    {
        // Open a file dialog to select the database path
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select Database File",
            Filter = "Database Files (*.db;*.sqlite)|*.db;*.sqlite|All Files (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() != true) return;
        _databasePath = openFileDialog.FileName;

        try
        {
            // Initialize the wrapper with the selected database path
            _eosWrapper = new WaterSteamEquationOfStateWrapper(_databasePath);

            // Enable the input controls
            SetControlsEnabled(true);

            MessageBox.Show("Database loaded successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load the database: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            _eosWrapper = null;
            SetControlsEnabled(false);
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        _eosWrapper?.Dispose();
        Close();
    }

    private void PlotButton_Click(object sender, RoutedEventArgs e)
    {
        if (_eosWrapper == null)
        {
            MessageBox.Show("Please set the database path before plotting.", "Database Not Set", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var selectedPropertyDescription = (PropertyComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
        if (selectedPropertyDescription == null) return;
        var selectedProperty = EnumHelper.GetEnumValueFromDescription<ThermodynamicProperty>(selectedPropertyDescription);
        _propertyUnits.TryGetValue(selectedProperty, out var units);
        var plotModel = new PlotModel
        {
            Title = $"{EquationOfStateName}: {selectedPropertyDescription}",
            Legends = { new Legend { LegendPosition = LegendPosition.RightTop } }
        };
        var yAxis = new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = $"{units}",
            MajorGridlineStyle = LineStyle.Solid,
            MinorGridlineStyle = LineStyle.Dot
        };
        plotModel.Axes.Add(yAxis);
        var selectedPlotTypeDescription = (PlotTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
        if (selectedPlotTypeDescription == null) return;
        var selectedPlotType = EnumHelper.GetEnumValueFromDescription<PlotTypes>(selectedPlotTypeDescription);
        LinearAxis? xAxis;
        switch (selectedPlotType)
        {
            case PlotTypes.IsoBar:
                xAxis = new LinearAxis
                {
                    Position = AxisPosition.Bottom,
                    Title = "Temperature (K)",
                    MajorGridlineStyle = LineStyle.Solid,
                    MinorGridlineStyle = LineStyle.Dot
                };
                plotModel.Axes.Add(xAxis);
                PlotIsoBars(plotModel, selectedProperty);
                break;
            case PlotTypes.IsoTherm:
                xAxis = new LinearAxis
                {
                    Position = AxisPosition.Bottom,
                    Title = "Pressure (MPa)",
                    MajorGridlineStyle = LineStyle.Solid,
                    MinorGridlineStyle = LineStyle.Dot
                };
                plotModel.Axes.Add(xAxis);
                PlotIsoTherms(plotModel, selectedProperty);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        PlotView.Model = plotModel;
    }

    private void AddIsoBarButton_Click(object sender, RoutedEventArgs e)
    {
        var pressure = PressureSlider.Value;
        var selectedPressureUnit = (PressureUnitComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
        if (selectedPressureUnit == null) return;

        var pressureInMPa = EnumHelper.GetEnumValueFromDescription<PressureUnit>(selectedPressureUnit) switch
        {
            PressureUnit.MPa => pressure,
            PressureUnit.kPa => pressure / 1000.0,
            PressureUnit.Pa => pressure / 1_000_000.0,
            _ => throw new Exception("Unsupported pressure unit.")
        };

        if (!IsoBarValues.Contains(pressureInMPa))
            IsoBarValues.Add(pressureInMPa);
    }

    private void AddIsoThermButton_Click(object sender, RoutedEventArgs e)
    {
        var temperature = TemperatureSlider.Value;
        var selectedTemperatureUnit = (TemperatureUnitComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
        if (selectedTemperatureUnit == null) return;

        var temperatureInK = EnumHelper.GetEnumValueFromDescription<TemperatureUnit>(selectedTemperatureUnit) switch
        {
            TemperatureUnit.K => temperature,
            TemperatureUnit.C => temperature + 273.15,
            _ => throw new Exception("Unsupported temperature unit.")
        };

        if (!IsoThermValues.Contains(temperatureInK))
            IsoThermValues.Add(temperatureInK);
    }

    private void RemoveIsoBarItem(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: double value }) return;
        IsoBarValues.Remove(value);
        PlotButton_Click(sender, e);
    }

    private void RemoveIsoThermItem(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: double value }) return;
        IsoThermValues.Remove(value);
        PlotButton_Click(sender, e);
    }

    private void PlotTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedPlotTypeString = (PlotTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
        if (selectedPlotTypeString == null) return;
        var selectedPlotType = EnumHelper.GetEnumValueFromDescription<PlotTypes>(selectedPlotTypeString);
        if (IsoBarInput == null || IsoThermInput == null) return;
        IsoBarInput.Visibility = selectedPlotType == PlotTypes.IsoBar ? Visibility.Visible : Visibility.Collapsed;
        IsoBarListBox.Visibility = selectedPlotType == PlotTypes.IsoBar ? Visibility.Visible : Visibility.Collapsed;
        IsoThermInput.Visibility = selectedPlotType == PlotTypes.IsoTherm ? Visibility.Visible : Visibility.Collapsed;
        IsoThermListBox.Visibility = selectedPlotType == PlotTypes.IsoTherm ? Visibility.Visible : Visibility.Collapsed;
        PlotView.ResetAllAxes();
    }

    private void PlotIsoBars(PlotModel plotModel, ThermodynamicProperty selectedProperty)
    {
        foreach (var item in IsoBarListBox.Items)
        {
            if (item is not double pressureInMPa) continue;

            var pressureArray = Enumerable.Repeat(pressureInMPa, 1000).ToArray();
            var maximumTemperature = pressureInMPa <= 50 ? MaxTemperatureBelow50MPa : MaxTemperatureAbove50MPa;
            var temperatureArray = Enumerable.Range(0, 1000)
                .Select(i => MinTemperatureK + i * (maximumTemperature - MinTemperatureK) / 999)
                .ToArray();

            var propertyArray = GetPropertyArray(selectedProperty, temperatureArray, pressureArray);
            var lineSeries = new LineSeries { Title = $"{pressureInMPa} MPa", MarkerType = MarkerType.None };

            for (var i = 0; i < temperatureArray.Length; i++)
                if (!double.IsNaN(propertyArray[i]))
                    lineSeries.Points.Add(new DataPoint(temperatureArray[i], propertyArray[i]));

            plotModel.Series.Add(lineSeries);
        }
    }

    private void PlotIsoTherms(PlotModel plotModel, ThermodynamicProperty selectedProperty)
    {
        foreach (var item in IsoThermListBox.Items)
        {
            if (item is not double temperatureInK) continue;
            var maximumPressure = temperatureInK <= MaxTemperatureAbove50MPa ? 100 : 50;
            var pressureArray = Enumerable.Range(0, 1000)
                .Select(i => MinPressureMPa + i * (maximumPressure - MinPressureMPa) / 999)
                .ToArray();
            var temperatureArray = Enumerable.Repeat(temperatureInK, pressureArray.Length).ToArray();
            var propertyArray = GetPropertyArray(selectedProperty, temperatureArray, pressureArray);
            var lineSeries = new LineSeries { Title = $"{temperatureInK} K", MarkerType = MarkerType.None };
            for (var i = 0; i < pressureArray.Length; i++)
                if (!double.IsNaN(propertyArray[i]))
                    lineSeries.Points.Add(new DataPoint(pressureArray[i], propertyArray[i]));

            plotModel.Series.Add(lineSeries);
        }
    }

    private double[] GetPropertyArray(ThermodynamicProperty property, double[] temperatures, double[] pressures)
    {
        if (_eosWrapper is null)
            throw new InvalidOperationException("The equation of state wrapper is not initialized.");
        return property switch
        {
            ThermodynamicProperty.Density => _eosWrapper.CalculateDensityArray(temperatures, pressures),
            ThermodynamicProperty.SpecificEnthalpy => _eosWrapper.CalculateSpecificEnthalpyArray(temperatures, pressures),
            ThermodynamicProperty.SpecificEntropy => _eosWrapper.CalculateSpecificEntropyArray(temperatures, pressures),
            ThermodynamicProperty.SpecificVolume => _eosWrapper.CalculateSpecificVolumeArray(temperatures, pressures),
            ThermodynamicProperty.SpecificInternalEnergy => _eosWrapper.CalculateSpecificInternalEnergyArray(temperatures, pressures),
            ThermodynamicProperty.SpecificIsobaricHeatCapacity => _eosWrapper.CalculateSpecificIsobaricHeatCapacityArray(temperatures, pressures),
            ThermodynamicProperty.SpecificIsochoricHeatCapacity => _eosWrapper.CalculateSpecificIsochoricHeatCapacityArray(temperatures, pressures),
            ThermodynamicProperty.SpeedOfSound => _eosWrapper.CalculateSpeedOfSoundArray(temperatures, pressures),
            _ => throw new Exception("Selected property is not implemented.")
        };
    }

    private void UpdatePressureSliderLimits()
    {
        var selectedUnit = EnumHelper.GetEnumValueFromDescription<PressureUnit>((PressureUnitComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? string.Empty);
        (PressureSlider.Minimum, PressureSlider.Maximum) = selectedUnit switch
        {
            PressureUnit.MPa => (MinPressureMPa, MaxPressureMPa),
            PressureUnit.kPa => (MinPressureMPa * 1000, MaxPressureMPa * 1000),
            PressureUnit.Pa => (MinPressureMPa * 1_000_000, MaxPressureMPa * 1_000_000),
            _ => throw new Exception("Unsupported pressure unit.")
        };
    }

    private void UpdateTemperatureSliderLimits()
    {
        var selectedUnit = EnumHelper.GetEnumValueFromDescription<TemperatureUnit>((TemperatureUnitComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? string.Empty);
        var maxTemperature = (PressureSlider.Value <= 50) ? MaxTemperatureBelow50MPa : MaxTemperatureAbove50MPa;
        (TemperatureSlider.Minimum, TemperatureSlider.Maximum) = selectedUnit switch
        {
            TemperatureUnit.K => (MinTemperatureK, maxTemperature),
            TemperatureUnit.C => (MinTemperatureK - 273.15, maxTemperature - 273.15),
            _ => throw new Exception("Unsupported temperature unit.")
        };
    }

    private void PressureUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePressureSliderLimits();
    }

    private void TemperatureUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateTemperatureSliderLimits();
    }

    #endregion

    #region Summary Properties I/O

    private void CalcPressureUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateCalcPressureSliderLimits();
    }

    private void CalcTemperatureUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateCalcTemperatureSliderLimits();
    }

    private void UpdateCalcPressureSliderLimits()
    {
        var selectedUnit = EnumHelper.GetEnumValueFromDescription<PressureUnit>((CalcPressureUnitComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? string.Empty);
        (CalcPressureSlider.Minimum, CalcPressureSlider.Maximum) = selectedUnit switch
        {
            PressureUnit.MPa => (MinPressureMPa, MaxPressureMPa),
            PressureUnit.kPa => (MinPressureMPa * 1000, MaxPressureMPa * 1000),
            PressureUnit.Pa => (MinPressureMPa * 1_000_000, MaxPressureMPa * 1_000_000),
            _ => throw new Exception("Unsupported pressure unit.")
        };
    }

    private void UpdateCalcTemperatureSliderLimits()
    {
        var selectedUnit = EnumHelper.GetEnumValueFromDescription<TemperatureUnit>((CalcTemperatureUnitComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? string.Empty);
        var maxTemperature = (CalcPressureSlider.Value <= 50) ? MaxTemperatureBelow50MPa : MaxTemperatureAbove50MPa;
        (CalcTemperatureSlider.Minimum, CalcTemperatureSlider.Maximum) = selectedUnit switch
        {
            TemperatureUnit.K => (MinTemperatureK, maxTemperature),
            TemperatureUnit.C => (MinTemperatureK - 273.15, maxTemperature - 273.15),
            _ => throw new Exception("Unsupported temperature unit.")
        };
    }

    private void CalculateProperties_Click(object sender, RoutedEventArgs e)
    {
        if (_eosWrapper == null)
        {
            MessageBox.Show("Please set the database path before calculating properties.", "Database Not Set", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var selectedPressureUnit = EnumHelper.GetEnumValueFromDescription<PressureUnit>((CalcPressureUnitComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? string.Empty);
        var selectedTemperatureUnit = EnumHelper.GetEnumValueFromDescription<TemperatureUnit>((CalcTemperatureUnitComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? string.Empty);
        var pressure = selectedPressureUnit switch
        {
            PressureUnit.MPa => CalcPressureSlider.Value,
            PressureUnit.kPa => CalcPressureSlider.Value / 1000.0,
            PressureUnit.Pa => CalcPressureSlider.Value / 1_000_000.0,
            _ => throw new Exception("Unsupported pressure unit.")
        };
        var temperature = selectedTemperatureUnit switch
        {
            TemperatureUnit.K => CalcTemperatureSlider.Value,
            TemperatureUnit.C => CalcTemperatureSlider.Value + 273.15,
            _ => throw new Exception("Unsupported temperature unit.")
        };
        double[] temperatures = [temperature];
        double[] pressures = [pressure];
        var properties = new Dictionary<ThermodynamicProperty, double>
    {
        { ThermodynamicProperty.Density, _eosWrapper.CalculateDensityArray(temperatures, pressures)[0] },
        { ThermodynamicProperty.SpecificEnthalpy, _eosWrapper.CalculateSpecificEnthalpyArray(temperatures, pressures)[0] },
        { ThermodynamicProperty.SpecificEntropy, _eosWrapper.CalculateSpecificEntropyArray(temperatures, pressures)[0] },
        { ThermodynamicProperty.SpecificVolume, _eosWrapper.CalculateSpecificVolumeArray(temperatures, pressures)[0] },
        { ThermodynamicProperty.SpecificInternalEnergy, _eosWrapper.CalculateSpecificInternalEnergyArray(temperatures, pressures)[0] },
        { ThermodynamicProperty.SpecificIsobaricHeatCapacity, _eosWrapper.CalculateSpecificIsobaricHeatCapacityArray(temperatures, pressures)[0] },
        { ThermodynamicProperty.SpecificIsochoricHeatCapacity, _eosWrapper.CalculateSpecificIsochoricHeatCapacityArray(temperatures, pressures)[0] },
        { ThermodynamicProperty.SpeedOfSound, _eosWrapper.CalculateSpeedOfSoundArray(temperatures, pressures)[0] }
    };
        DisplayProperties(properties);
    }

    private void DisplayProperties(Dictionary<ThermodynamicProperty, double> properties)
    {
        PropertiesResultStack.Children.Clear();
        foreach (var label in from property in properties
                              let unitLabel = _propertyUnits[property.Key]
                              select new Label { Content = $"{property.Key}: {property.Value:F2} {unitLabel}" })
        {
            PropertiesResultStack.Children.Add(label);
        }
    }

    #endregion
}