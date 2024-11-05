# NilsInfinite.EquationOfStateViewer

## Overview
NilsInfinite.EquationOfStateViewer is a sample tool designed to visualize the IAPWS-IF97 equation of state for water via the [NilsInfinite.EquationOfState](https://github.com/NilsInfiniteAnalytics/NilsInfinite.EquationsOfState) project.

It provides a user-friendly interface to input parameters for generating graphical representations of the iso-bar and iso-therm properties and quick calculation pad for calculating all the properties at a pressure and temperature.

#### Check the releases for accessing the application compiled binaries!

## Features
- **Input Parameters**: Temperature and pressure inputs for visualizing the equation of state Regions 1-5 in the IF97 EOS.
- **Visualization**: Generate and view graphical representations using [Oxyplot](https://github.com/oxyplot/oxyplot)

## Features coming!
- **Export**: Export the results and graphs for further analysis.
- **Axis manipulation**: Customize the P and T axes for finer visualizations.

## Local Build Steps
1. Ensure you have the [NilsInfinite.EquationOfState](https://github.com/NilsInfiniteAnalytics/NilsInfinite.EquationsOfState) project pulled and built.
2. Adjust the path to the WaterSteamEquationOfState.dll to your built file location. Add to the project if it's not available.
3. Ensure the dll has a Content Build Action, and Copy To Output Directory is set to Copy Always.
4. Ensure Viewer is set as the start-up project.
5. Run!

## Contributing
Contributions are welcome! If you have any suggestions or improvements, please create a pull request or open an issue.

## License
This project is licensed under the MIT License. See the [LICENSE](LICENSE.txt) file for more details.

## Contact
For any questions or inquiries, feel free to contact me.
