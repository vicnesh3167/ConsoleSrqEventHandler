using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ivi.Visa;
using Ivi.Visa.FormattedIO;
using Keysight.Visa; // has some kind of issue with versioning with IOLS 2025 install and the whole .NET 6+ thing if you pick Ivi.Visa 7.2.0 but okay with 8.0.0
//using NationalInstruments.Visa; // with method 2, cannot cast Keysight.Visa.UsbSession event callback to NationalInstruments.Visa.MessageBasedSession object
using System.Threading;

namespace ConsoleSrqEventHandler
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("C#.NET Console App - VISA.NET ServiceRequest Blocking and Event Handling with 34470A DMM ...");

            // Change this variable to the address of your instrument
            string VISA_ADDRESS = "MyInstr";

            IMessageBasedSession session = null;
            MessageBasedFormattedIO formattedIO;
                        

            // Shows the mechanics of how to catch an exception (an error) in VISA.NET when it occurs. 
            // To stimulate an error, you can provide an invalid address...
            try
            {
                // Before running this part, don't forget to set your instrument address in the 'VISA_ADDRESS' variable at the top of this method
                session = GlobalResourceManager.Open(VISA_ADDRESS) as IMessageBasedSession;
                // Set session VISA timeout
                session.TimeoutMilliseconds = 5000;
                formattedIO = new MessageBasedFormattedIO(session);

                formattedIO.WriteLine("*IDN?");
                string idnResponse = formattedIO.ReadLine();
                Console.WriteLine("*IDN? returned: {0}", idnResponse);

                // Issue a Device Clear for the session 
                session.Clear();

                /* Two examples of SRQ handling are shown here, pick either the blocking method or the Event Handling method */
                Console.WriteLine("\nType \"1\" for a Blocking Service Request Example OR Type \"2\" for Event Handling a Service Request Example:");
                string userChoice = Console.ReadLine();

                /* Initialize the DMM and get it ready to make a measurement, then trigger it */
                // Randomize sample size to change how long WaitOnEvent(SRQ) takes
                Random rng = new Random();
                int readingCount = rng.Next(20, 2000);
                Console.WriteLine("\nRequesting sample count: {0}", readingCount);

                formattedIO.WriteLine("*RST");
                formattedIO.WriteLine("*CLS");
                formattedIO.WriteLine("*SRE 32; *ESE 1"); //enable status register masks appropriately for each model of instrument
                formattedIO.WriteLine("FORMAT:DATA REAL");
                formattedIO.WriteLine("FORMAT:BORDER SWAPPED");
                formattedIO.WriteLine("CONFIGURE:VOLT:DC 10,1E-6");
                formattedIO.WriteLine($"SAMPLE:COUNT {readingCount}");
                formattedIO.WriteLine("TRIG:SOUR BUS");

                formattedIO.WriteLine("INIT; *OPC"); //init measurement with *OPC so all configurations are completed and DMM waits for a trigger

                // Trigger DMM with random delay to create a variance of when the instrument is triggered and then ready with data to issue an SRQ                
                int delay = rng.Next(125, 10550);
                Thread.Sleep(delay);
                Console.WriteLine("*TRG delayed for: {0} s", Decimal.Divide(delay, 1000));
                formattedIO.WriteLine("*TRG");               

                if (userChoice == "1")
                {
                    /* Blocking type of Service Request Example */                                                                  
                    
                    // Enable SRQ event, Wait for SRQ event to be raised by instrument or timeout, and then read the measurement(s) from the instrument
                    // Check each instruments programming guide to determine how the behave for each measurement or waveform query and how they raise a Service Request
                    session.EnableEvent(EventType.ServiceRequest);
                    formattedIO.WriteLine("FETCH?");
                    // (Blocking SRQ example) Wait here until instrument issues an SRQ which means data is ready to be read back 
                    var timerStart = DateTime.Now;
                    session.WaitOnEvent(EventType.ServiceRequest, 80000); // set this SRQ WaitOnEvent timeout (80 s) much longer that default timeout (5 s) because depending on sample count, it can take much, much longer for the data to be ready                              
                    var timerStop = DateTime.Now - timerStart;
                    Console.WriteLine($"WaitOnEvent(SRQ) took: {timerStop.TotalSeconds} s\n");
                    session.DisableEvent(EventType.ServiceRequest);

                    // Reading data only after it is ready at the instrument and displaying binary data using formattedIO methods
                    var byteArray = new Byte[readingCount * 8];
                    long actualByteArrayLength = formattedIO.ReadBinaryBlockOfByte(byteArray, 0, byteArray.Length);
                    Console.WriteLine("Received {0} bytes or {1} samples from instrument:\n{2}", actualByteArrayLength, actualByteArrayLength / 8, BitConverter.ToString(byteArray));
                }
                else if (userChoice == "2")
                {
                    /* Event Handling a Service Request Example */
                    // Use SynchronizeCallbacks to specify that the object marshals callbacks across threads appropriately
                    session.SynchronizeCallbacks = true;
                    // Registering a handler for an event automatically enables that event
                    session.ServiceRequest += OnServiceRequest;

                    Console.WriteLine("\r\nProgram waiting here for ServiceRequest event to occur, which would be handled by OnServiceRequest...\nOnce you see data, press Enter to continue.\n");
                    Console.ReadLine();

                    // Unregistering the handler which also disables the event
                    session.ServiceRequest -= OnServiceRequest;
                }
                else
                {
                    Console.WriteLine("Invalid choice entered!");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("An exception has occurred!\r\n\r\n{0}\r\n", ex.ToString());
                session.Clear(); // clear the instrument session so next time the program can run without issues from previous run
                
                // To get more specific information about the exception, we can check what kind of exception it is and add specific error handling code
                // In this example, that is done in the ExceptionHandler method
                ExceptionHandler(ex);
            }


            // Delete session object
            if (session != null)
            {
                session.Dispose();
            }

            Console.WriteLine("\r\nPress any key to exit...");
            Console.ReadKey();
        }

        // Exception error handler
        static void ExceptionHandler(Exception ex)
        {
            // This is an example of accessing VISA.NET exceptions
            if (ex is IOTimeoutException)
            {
                Console.WriteLine("A timeout has occurred!\r\n");
            }
            else if (ex is NativeVisaException)
            {
                Console.WriteLine("A native VISA exception has occurred!\r\n");

                // To get more information about the error look at the ErrorCode property by 
                //     typecasting the generic exception to the more-specific Native VISA Exception    
                int errorCode = (ex as NativeVisaException).ErrorCode;
                Console.WriteLine("\r\n\tError code: {0}\r\n\tError name: {1}\r\n",
                    errorCode,
                    NativeErrorCode.GetMacroNameFromStatusCode(errorCode));
            }
            else if (ex is VisaException)
            {
                Console.WriteLine("A VISA exception has occurred!\r\n");
            }
            else
            {
                Console.WriteLine("Some other type of exception occurred: {0}\r\n", ex.GetType());
            }
        }

        static void OnServiceRequest(object sender, VisaEventArgs e)
        {
            // Must add reference to Keysight.Visa or NationalInstruments.Visa to use (MessageBasedSession) here
            // Each vendor's specific VISA.NET library manages the Event Callback
            var session = (MessageBasedSession) sender;
            MessageBasedFormattedIO formattedIO = new MessageBasedFormattedIO(session);
            int defaultReadingCount = 50000;

            if (session != null)
            {
                try
                {
                    //Disable event while processing the callback
                    session.DisableEvent(EventType.ServiceRequest);

                    //Get status byte by performing serial poll
                    StatusByteFlags statusByte = session.ReadStatusByte();

                    //Based on the status registers and how the instrument updates them when it has data, manage the reading of that data in the conditional statements below
                    if ((statusByte & StatusByteFlags.MessageAvailable) != 0)
                    {
                        Console.WriteLine("\nMAV in status register is set.");
                        
                    }
                    else if ((statusByte & StatusByteFlags.EventStatusRegister) != 0)
                    {
                        Console.WriteLine("\nESB in status register is set.");
                        //Get content of event status register
                        session.RawIO.Write("*ESR?\n");
                        string esrValue = session.RawIO.ReadString();
                        Console.WriteLine("ESR value: {0}", esrValue);
                        //Read data back from instrument here. Couldn't pass ReadingCount from Main program to this handler to managing it in a crude way below
                        formattedIO.WriteLine("FETCH?");
                        var byteArray = new Byte[defaultReadingCount * 8];
                        long actualByteArrayLength = formattedIO.ReadBinaryBlockOfByte(byteArray, 0, byteArray.Length);
                        var byteArrayActual = new Byte[actualByteArrayLength];
                        for (int i = 0; i < actualByteArrayLength; i++)
                        {
                            byteArrayActual[i] = byteArray[i];
                        }
                        Console.WriteLine("Received {0} bytes or {1} samples from instrument:\n{2}", actualByteArrayLength, actualByteArrayLength / 8, BitConverter.ToString(byteArrayActual));
                    }

                    //Clear status registers in instrument
                    session.RawIO.Write("*CLS\n");
                }
                catch (Exception ex)
                {

                    Console.WriteLine("OnServiceRequest exception occured: {0}\r\n", ex.ToString());
                }                
            }
            else
            {
                Console.WriteLine("Sender is not a valid reason.");
            }
        }
    }
}
