This is a C#.NET Console project showing two ways to manage Service Request events from an instrument using VISA.NET library methods.
Option 1: Blocking method that uses session.WaitOnEvent() method to pause the main program while waiting for the instrument to raise a Service Request. 
Option 2: Separate Service Request event handler was created named OnServiceRequest. The main program session.ServiceRequest event was attached to this handler so when the callback occurs from the instrument, it is handled appropriately based on the status register values.

Tested with:
Keysight 34470A DMM - each instrument and method of acquiring data (fetch, read, measurement or wavefrom) can show different Service Request or status register behaviors. Consult each instrument's programming manual for details.
USB interface using VISA address shown in Keysight Connection Expert with Alias = MyInstr
IOLS 2025
VISA Shared Component 8.0.0
VISA.NET Shared Component 8.0.1
Referenced the Ivi.Visa 8.0.0.0 and Keysight.Visa 18.5.0.0 assemblies in this Console (.NET Framework) Project
Visual Studio 2022

Initial commit May 7th, 2025 by Vicnesh Nathan
