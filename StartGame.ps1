param(
	[switch]$stopExisting=$false,
	[int]$numConsoleClients=0,
	[int]$numPythonClients=0,
	[int]$num1400Clients=0,
	[string]$secretCode="banana55",
	[int]$serverPort=5000
)

if($stopExisting) {
	write-host "Killing existing dotnet and python* processes..."
	get-process dotnet* | stop-process
	get-process python* | stop-process
}

 ########################################
 write-host "Compiling risk server..."
 dotnet build Risk.Server

 ########################################
 write-host "Starting up risk server" -foregroundcolor green
 while((test-netconnection localhost -port $serverport -WarningAction SilentlyContinue).TcpTestSucceeded){
 	write-host "Port $serverport is in use, trying next port"
 	$serverport++
 }
 start-process dotnet -argumentlist "run","--project","./Risk.Server","--StartGameCode", $secretCode, "--urls", "http://localhost:$serverport"
 start-process "http://localhost:$serverport"

if($numConsoleClients -gt 0) {
	########################################
	write-host "Compiling console client..."
	dotnet build risk.signalr.consoleclient

	########################################
	write-host "Starting C# ConsoleClients:"
	foreach($i in 1..$numConsoleClients){
		write-host "Starting up console-based competitor #$i...`n" -foregroundcolor green
		start-sleep -seconds 1
		start-process dotnet -argumentlist "run","--project","./Risk.Signalr.ConsoleClient","--serverAddress","http://localhost:$serverport","--playerName","Console $i" -windowStyle Minimized
	}
}

if($numPythonClients -gt 0) {
	########################################
	write-host "Installing Python Dependencies"
	python -m pip install -r ./Risk.SignalR.PythonClient/requirements.txt
	if($LASTEXITCODE -ne 0) {
		write-warning "Unable to install python dependencies.  Skipping python clients."
	} else {
		########################################
		write-host "Starting Python Clients"
		foreach($i in 1..$numPythonClients){
			write-host "Starting up Python competitor #$i...`n" -foregroundcolor green
			start-sleep -seconds 1
			start-process python -argumentlist "./Risk.SignalR.PythonClient/SampleRiskClient.py" -windowStyle Minimized
		}
	}
}

if($num1400Clients -gt 0) {
	########################################
	write-host "Compiling console client..."
	dotnet build risk.signalr.CS1400Client

	########################################
	write-host "Starting CS1400 Clients"
	foreach($i in 1..$num1400Clients){
		write-host "Starting up CS1400 competitor #$i...`n" -foregroundcolor green
		start-sleep -seconds 1
		start-process dotnet -argumentlist "run","--project","./Risk.Signalr.CS1400Client","http://localhost:$serverport","CS1400 $i" -windowStyle Minimized
	}
}

