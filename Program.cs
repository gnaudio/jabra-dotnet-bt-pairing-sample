using System.Reactive.Linq;
using Jabra.NET.Sdk.BluetoothPairing;
using Jabra.NET.Sdk.Core;
using Jabra.NET.Sdk.Core.Types;
using Jabra.NET.Sdk.Properties;

internal class Program
{
    public static BluetoothPairingFactory? btPairingFactory;
    public static IBluetoothDongle? activeDongle;

    // Latest list of scan entries when searching for devices
    public static List<IScanEntry> scanEntries = [];
    // Latest list of paired devices
    public static IReadOnlyList<IPairingListEntry> pairingList = [];

    public static bool keypress_a_to_z_means_pairing = false; 

    public static async Task Main()
    {
        // Writing available demo commands to console 
        PrintMenu();

        // Start the key press listener in a separate task
        var keyPressTask = Task.Run(() => ListenForKeyPress(), CancellationToken.None);

        

        //Initialize the core SDK. Recommended to use Init.InitManualSdk(...) (not Init.Init(...)) to allow setup of listeners before the SDK starts discovering devices.
        var config = new Config(
            partnerKey: "get-partner-key-at-developer.jabra.com",
            appId: "JabraDotNETBluetoothPairingSample",
            appName: "Jabra .NET Bluetooth Pairing Sample"
        );
        IManualApi jabraSdk = Init.InitManualSdk(config);
        
        //Subscribe to SDK log events.
        jabraSdk.LogEvents.Subscribe((log) =>
        {
            if (log.Level == LogLevel.Error) Console.WriteLine(log.ToString());
            //Ignore info, warning, and debug log messages.
        });

        // Create a BluetoothPairingFactory
        btPairingFactory = new BluetoothPairingFactory(jabraSdk);

        //Setup listeners for Jabra devices being attached/detected.
        SetupDeviceListeners(jabraSdk);

        // Enable the SDK's device discovery AFTER listeners and other necessary infrastructure is setup.
        await jabraSdk.Start();
        Console.WriteLine("Now listening for Jabra devices...\n");

        //Keep the sample app running until actively closed.
        Task.Delay(-1).Wait();
    }

    static async Task ListAllPairingsAsync()
    {
        // Keypress a to z means try to connect/disconnect selected device
        keypress_a_to_z_means_pairing = false; 

        pairingList = await activeDongle.GetPairingList();
        Console.WriteLine($"Current pairing list for {activeDongle.Name}: ({pairingList.Count} entries)");
        int i = 0;
        foreach (var entry in pairingList)
        {
            Console.WriteLine($"> {entry.BluetoothName}, connected: {entry.ConnectionStatus.ToString()} - to connect/disconnect press key '{(char)(i + 97)}'");
            i++;
        }
    } 

    static async Task RemoveAllPairingsAsync()
    {
        IReadOnlyList<IPairingListEntry> pairingList = await activeDongle.GetPairingList();
        bool success = true;
        Console.WriteLine($"Trying to remove all pairings for {activeDongle.Name}");
        foreach (var entry in pairingList)
        {
            try
            {
                Console.WriteLine($"> Removing {entry.BluetoothName}");
                if (!(entry.ConnectionStatus == BluetoothConnectionStatus.NONE) || (entry.ConnectionStatus == BluetoothConnectionStatus.DISCONNECTED))
                {
                    // Disconnect from device, if connection status is not NONE or DISCONNECTED. 
                    await activeDongle.DisconnectFrom(entry);
                }
                await activeDongle.Unpair(entry);
                Console.WriteLine($"> Successfully removed {entry.BluetoothName}");
            }
            catch (Exception ex)
            {
                // An exception happened while trying to unpair. One common reason is if the dongle is
                // currently connected to the device you're trying to unpair. To avoid this, always disconnect
                // before attempting to unpair. 
                Console.WriteLine($"Exception while removing pairing: {ex.Message}");
                success = false;
            }

        }
        Console.WriteLine($"All pairings for {activeDongle.Name} successfully removed: {success}");
        
    }

    static async Task StartBTPairingProcess()
    {
        // Keypress a to z in demo app switches to initiate pairing of selected device
        keypress_a_to_z_means_pairing = true;
        
        // Init scanEntries
        scanEntries = [];
        // Default scan duration is 20 seconds. Scan will stop also once a pairing is initiated. 
        activeDongle.ScanForDevicesInPairingMode().Subscribe(entry =>
        {
            Console.WriteLine($"> Found device {entry.BluetoothName} - to pair press key '{(char)(scanEntries.Count + 97)}'");
            scanEntries.Add(entry);
        });
    }

    static async Task PairWithDevice(int deviceIndexInList)
    {
        Console.WriteLine($"Attempting to pair dongle {activeDongle.Name} with {scanEntries[deviceIndexInList].BluetoothName}");
        // Try to pair - exception will be thrown if pairing is not successfull within timeout indicated. Pairing likely still works after that
        // but it is recommended to set a generous timeout (e.g. 30 seconds) to avoid exceptions due to slow pairing. 
        bool success = true;
        try
        {
            await activeDongle.PairAndConnectTo(scanEntries[deviceIndexInList], TimeSpan.FromSeconds(30));
        } catch (Exception ex)
        {
            Console.WriteLine($"Exception during pairing: {ex.Message}");
            success = false; 
        }
        if (!success) { activeDongle.ConnectTo(scanEntries[deviceIndexInList].BluetoothAddress);  }
        Console.WriteLine($"Pairing successfull within timeout: {success}");
        ListAllPairingsAsync();
    }

    static async Task Connect_Disconnect(int deviceIndexInList)
    {
        if ((pairingList[deviceIndexInList].ConnectionStatus == BluetoothConnectionStatus.CONNECTED) || (pairingList[deviceIndexInList].ConnectionStatus == BluetoothConnectionStatus.NONE))
        {
            Console.WriteLine($"Disconnecting from {pairingList[deviceIndexInList].BluetoothName}");
            await activeDongle.DisconnectFrom(pairingList[deviceIndexInList]);
            await ListAllPairingsAsync();
        } else
        {
            Console.WriteLine($"Connecting to {pairingList[deviceIndexInList].BluetoothName}");
            await activeDongle.ConnectTo(pairingList[deviceIndexInList]);
            await ListAllPairingsAsync();
        }
    }

    static void SetupDeviceListeners(IApi jabraSdk)
    {
        //Subscribe to Jabra devices being attached/detected by the SDK
        jabraSdk.DeviceAdded.Subscribe(async (IDevice device) =>
        {
            // This sample is supported for Jabra Link 380 and Jabra Link 390 BT dongles
            if ((device.Name == "Jabra Link 380") || (device.Name == "Jabra Link 390"))
            {
                if (await btPairingFactory.CreateBluetoothDongle(device) is IBluetoothDongle dongle)
                {
                    // IDevice device is a dongle...
                    activeDongle = dongle;
                    Console.WriteLine($"Found Jabra BT dongle: {dongle.Name}");
                    await ListAllPairingsAsync();
                }
            }
            
        });
    }

    /************************************
    ** Methods for setting up demo app **
    ************************************/
    static void PrintMenu()
    {
        Console.WriteLine("Jabra .NET SDK Bluetooth Pairing Sample app starting. Press ctrl+c or close the window to end.\n");
        Console.WriteLine("----------");
        Console.WriteLine("Available commands when a Jabra Link 380/390 BT dongle is detected:");
        Console.WriteLine("1: List paired devices");
        Console.WriteLine("2: Remove all paired devices");
        Console.WriteLine("3: Pair new BT device");
        Console.WriteLine("----------");
    }
    async static void HandleKeyPress(char keyChar)
    {
        switch (keyChar)
        {
            case '1':
                // List all devices currently paired with the BT dongle
                await ListAllPairingsAsync();
                break;
            case '2':
                // Remove all devices currently paired with the BT dongle
                await RemoveAllPairingsAsync();
                break;
            case '3':
                // Put dongle into pairing mode and wait for nearby BT devices to be found. 
                Console.WriteLine($"Setting dongle {activeDongle.Name} to search for nearby BT devices. Make sure to put your device in pairing mode.");
                StartBTPairingProcess();
                break;
            
            default:
                // if keypress was between 'a' and 'z', treat it as pairing to one of the listed devices. 'a' is ascii 97. 
                if ((((int)keyChar) > 96) && (((int)keyChar) < 123))
                {
                    if (keypress_a_to_z_means_pairing)
                    {
                        PairWithDevice((int)keyChar - 97);
                    }
                    else
                    {
                        Connect_Disconnect((int)keyChar - 97); 

                    }

                }
                break;
        }
    }

    static void ListenForKeyPress()
    {
        while (true)
        {
            if (Console.KeyAvailable) // Check if a key press is available
            {
                var keyInfo = Console.ReadKey(intercept: true);
                HandleKeyPress(keyInfo.KeyChar);
            }
            else
            {
                // Sleep briefly to avoid busy waiting
                Thread.Sleep(50);
            }
        }
    }
}
