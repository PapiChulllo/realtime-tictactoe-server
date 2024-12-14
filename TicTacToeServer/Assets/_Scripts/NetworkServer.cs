using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using System.Text;

public class NetworkServer : MonoBehaviour
{
    public NetworkDriver networkDriver;
    private NativeList<NetworkConnection> networkConnections;

    NetworkPipeline reliableAndInOrderPipeline;
    NetworkPipeline nonReliableNotInOrderedPipeline;

    const ushort NetworkPort = 9001;

    const int MaxNumberOfClientConnections = 1000;

    private int[,] gameBoard = new int[3, 3]; // 0 = empty, 1 = Player 1, 2 = Player 2
    private int currentPlayer = 1; // Player 1 starts
    private bool gameActive = true;

    void Start()
    {
        networkDriver = NetworkDriver.Create();
        reliableAndInOrderPipeline = networkDriver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
        nonReliableNotInOrderedPipeline = networkDriver.CreatePipeline(typeof(FragmentationPipelineStage));
        NetworkEndpoint endpoint = NetworkEndpoint.AnyIpv4;
        endpoint.Port = NetworkPort;

        int error = networkDriver.Bind(endpoint);
        if (error != 0)
            UnityEngine.Debug.Log("Failed to bind to port " + NetworkPort);
        else
            networkDriver.Listen();

        networkConnections = new NativeList<NetworkConnection>(MaxNumberOfClientConnections, Allocator.Persistent);

        ResetGame(); // Initialize the game state
    }

    void OnDestroy()
    {
        networkDriver.Dispose();
        networkConnections.Dispose();
    }

    void Update()
    {
        networkDriver.ScheduleUpdate().Complete();

        for (int i = 0; i < networkConnections.Length; i++)
        {
            if (!networkConnections[i].IsCreated)
            {
                networkConnections.RemoveAtSwapBack(i);
                i--;
            }
        }

        while (AcceptIncomingConnection())
        {
            UnityEngine.Debug.Log("Accepted a client connection");
        }

        DataStreamReader streamReader;
        NetworkPipeline pipelineUsedToSendEvent;
        NetworkEvent.Type networkEventType;

        for (int i = 0; i < networkConnections.Length; i++)
        {
            if (!networkConnections[i].IsCreated)
                continue;

            while (PopNetworkEventAndCheckForData(networkConnections[i], out networkEventType, out streamReader, out pipelineUsedToSendEvent))
            {
                switch (networkEventType)
                {
                    case NetworkEvent.Type.Data:
                        int sizeOfDataBuffer = streamReader.ReadInt();
                        NativeArray<byte> buffer = new NativeArray<byte>(sizeOfDataBuffer, Allocator.Persistent);
                        streamReader.ReadBytes(buffer);
                        byte[] byteBuffer = buffer.ToArray();
                        string msg = Encoding.Unicode.GetString(byteBuffer);
                        ProcessReceivedMsg(msg, networkConnections[i]);
                        buffer.Dispose();
                        break;
                    case NetworkEvent.Type.Disconnect:
                        UnityEngine.Debug.Log("Client has disconnected from server");
                        networkConnections[i] = default(NetworkConnection);
                        break;
                }
            }
        }
    }

    private bool AcceptIncomingConnection()
    {
        NetworkConnection connection = networkDriver.Accept();
        if (connection == default(NetworkConnection))
            return false;

        networkConnections.Add(connection);
        return true;
    }

    private bool PopNetworkEventAndCheckForData(NetworkConnection networkConnection, out NetworkEvent.Type networkEventType, out DataStreamReader streamReader, out NetworkPipeline pipelineUsedToSendEvent)
    {
        networkEventType = networkConnection.PopEvent(networkDriver, out streamReader, out pipelineUsedToSendEvent);

        if (networkEventType == NetworkEvent.Type.Empty)
            return false;
        return true;
    }

    private void ProcessReceivedMsg(string msg, NetworkConnection sender)
    {
        UnityEngine.Debug.Log("Msg received = " + msg);

        if (msg.StartsWith("MOVE"))
        {
            string[] parts = msg.Split('|');
            if (parts.Length == 4)
            {
                int player = int.Parse(parts[1]);
                int x = int.Parse(parts[2]);
                int y = int.Parse(parts[3]);
                ProcessGameMove(player, x, y);
            }
        }
    }

    public void SendMessageToClient(string msg, NetworkConnection networkConnection)
    {
        byte[] msgAsByteArray = Encoding.Unicode.GetBytes(msg);
        NativeArray<byte> buffer = new NativeArray<byte>(msgAsByteArray, Allocator.Persistent);

        DataStreamWriter streamWriter;
        networkDriver.BeginSend(reliableAndInOrderPipeline, networkConnection, out streamWriter);
        streamWriter.WriteInt(buffer.Length);
        streamWriter.WriteBytes(buffer);
        networkDriver.EndSend(streamWriter);

        buffer.Dispose();
    }

    public void SendToAllClients(string msg)
    {
        for (int i = 0; i < networkConnections.Length; i++)
        {
            if (networkConnections[i].IsCreated)
            {
                SendMessageToClient(msg, networkConnections[i]);
            }
        }
    }

    private void ResetGame()
    {
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                gameBoard[i, j] = 0;

        currentPlayer = 1;
        gameActive = true;
    }

    private string SerializeGameState()
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                sb.Append(gameBoard[i, j]);
                if (j < 2) sb.Append(",");
            }
            if (i < 2) sb.Append(";");
        }
        sb.Append("|").Append(currentPlayer).Append("|").Append(gameActive);
        return sb.ToString();
    }

    private void ProcessGameMove(int player, int x, int y)
    {
        if (!gameActive || gameBoard[x, y] != 0 || player != currentPlayer)
            return;

        gameBoard[x, y] = player;
        if (CheckWinCondition(player))
        {
            gameActive = false;
            SendToAllClients($"WIN|{player}");
        }
        else if (CheckDrawCondition())
        {
            gameActive = false;
            SendToAllClients("DRAW");
        }
        else
        {
            currentPlayer = currentPlayer == 1 ? 2 : 1;
        }

        SendToAllClients(SerializeGameState());
    }

    private bool CheckWinCondition(int player)
    {
        for (int i = 0; i < 3; i++)
            if (gameBoard[i, 0] == player && gameBoard[i, 1] == player && gameBoard[i, 2] == player)
                return true;

        for (int j = 0; j < 3; j++)
            if (gameBoard[0, j] == player && gameBoard[1, j] == player && gameBoard[2, j] == player)
                return true;

        if (gameBoard[0, 0] == player && gameBoard[1, 1] == player && gameBoard[2, 2] == player)
            return true;

        if (gameBoard[0, 2] == player && gameBoard[1, 1] == player && gameBoard[2, 0] == player)
            return true;

        return false;
    }

    private bool CheckDrawCondition()
    {
        foreach (var cell in gameBoard)
            if (cell == 0) return false;

        return true;
    }
}
