using System.Reactive.Linq;
using Jabra.NET.Sdk.BluetoothPairing;
using Jabra.NET.Sdk.Core;
using Jabra.NET.Sdk.Core.Types;
using Jabra.NET.Sdk.Properties;

internal class Program
{
    public static BluetoothPairingFactory? btPairingFactory;
    public static IBluetoothDongle? activeDongle;

    public static async Task Main()
    {
        Console.WriteLine("Jabra .NET SDK Bluetooth Pairing Sample app starting. Press ctrl+c or close the window to end.\n");

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
        IReadOnlyList<IPairingListEntry> pairingList = await activeDongle.GetPairingList();
        Console.WriteLine($"Current pairing list for {activeDongle.Name}:");

        foreach (var entry in pairingList)
        {
            Console.WriteLine($"> {entry.BluetoothName}, connected: {entry.ConnectionStatus.ToString()}");
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



    static void SetupDeviceListeners(IApi jabraSdk)
    {
        //Subscribe to Jabra devices being attached/detected by the SDK
        jabraSdk.DeviceAdded.Subscribe(async (IDevice device) =>
        {
            Console.WriteLine($"> Device attached/detected: {device.Name} (Product ID: {device.ProductId}, Serial #: {device.SerialNumber})");

            // This sample is supported for Jabra Link 380 and Jabra Link 390 BT dongles
            if ((device.Name == "Jabra Link 380") || (device.Name == "Jabra Link 390"))
            {
                if (await btPairingFactory.CreateBluetoothDongle(device) is IBluetoothDongle dongle)
                {
                    // IDevice device is a dongle...
                    activeDongle = dongle;
                    Console.WriteLine($"Active dongle is: {dongle.Name}");
                    
                    await ListAllPairingsAsync();
                    await RemoveAllPairingsAsync();
                    await ListAllPairingsAsync();
                }
            }
            
        });

        //Subscribe to Jabra devices being detached/rebooted
        jabraSdk.DeviceRemoved.Subscribe((IDevice device) =>
        {
            Console.WriteLine($"< Device detached {device.Name} (Product ID: {device.ProductId}, Serial #: {device.SerialNumber})");
        });
    }
}
