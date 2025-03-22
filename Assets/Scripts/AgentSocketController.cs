using UnityEngine;
using UnityEngine.UI;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System;
using System.Collections.Concurrent;

public class AgentSocketController : MonoBehaviour
{
    [SerializeField] private float speed = 20f;
    // [SerializeField] private float rotationSpeed = 100f;
    // [SerializeField] private float jumpForce = 5f;
    private Rigidbody rb;
    private TcpListener server;
    private bool isRunning = true;
    [SerializeField] private Transform target;
    [SerializeField] private Material hitWallMaterial, hitTargetMaterial;
    [SerializeField] private MeshRenderer groundRenderer;
    private bool hitWall, hitTarget = false;
    //Thread-safe queue for commands
    private ConcurrentQueue<string> commadQueue = new ConcurrentQueue<string>();
    //Thread-safe queue for position requests
    private ConcurrentQueue<TcpClient> positionRequestQueue = new ConcurrentQueue<TcpClient>();


    void Start()
    {
        rb = GetComponent<Rigidbody>();
        Thread serverThread = new Thread(StartServer);
        serverThread.IsBackground = true;
        serverThread.Start();
    }

    void StartServer()
    {
        try
        {
            server = new TcpListener(IPAddress.Any, 5005);
            server.Start();
            Debug.Log("Server started on port 5005");

            while (isRunning)
            {
                TcpClient client = server.AcceptTcpClient();
                Debug.Log("Client Connected.");
                Thread clientThread = new Thread(() => HandleClient(client));
                clientThread.IsBackground = true;
                clientThread.Start();

            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Server Error: {e}");
        }
        finally
        {
            server.Stop();
        }
    }

    private void HandleClient(TcpClient client)
    {
        try
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string command = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            // Debug.Log($"Received Command: {command}");

            if (command == "get_positions")
            {
                positionRequestQueue.Enqueue(client);
            }
            else
            {
                commadQueue.Enqueue(command);
            }

        }
        catch (Exception e)
        {
            Debug.LogError($"Client handling error: {e}");
            client.Close();
        }

    }

    void Update()
    {
        if (commadQueue.TryDequeue(out string command))
        {
            ProcessCommand(command);
        }

        // Process position requests from the queue
        if (positionRequestQueue.TryDequeue(out TcpClient client))
        {
            SendPositions(client);
        }
    }


    void ProcessCommand(string command)
    {
        switch (command)
        {
            case "forward":
                transform.Translate(Vector3.forward * speed * Time.deltaTime);
                break;
            case "left":
                transform.Translate(Vector3.left * speed * Time.deltaTime);
                break;
            case "right":
                transform.Translate(Vector3.right * speed * Time.deltaTime);
                break;
            // case "jump":
            //     rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            //     break;
            case "back":
                transform.Translate(Vector3.back * speed * Time.deltaTime);
                break;
            case "reset":
                transform.localPosition = new Vector3(0, 0, 0);
                // Give random position to target
                target.localPosition = new Vector3(UnityEngine.Random.Range(-13, 13), 0f, 0f);
                hitWall = false;
                hitTarget = false;
                break;
        }
    }

    private void SendPositions(TcpClient client)
    {
        try
        {
            NetworkStream stream = client.GetStream();
            string positionData = $"{transform.localPosition.x},{transform.localPosition.y},{transform.localPosition.z},"
                                    + $"{target.localPosition.x},{target.localPosition.y},{target.localPosition.z},"
                                    + $"{hitWall},{hitTarget}";
            // Debug.Log($"Sending positions: {positionData}"); // Add this line
            byte[] response = Encoding.ASCII.GetBytes(positionData);
            stream.Write(response, 0, response.Length);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error Sending Positions: {e}");
        }
        finally
        {
            client.Close();
        }

    }


    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            Debug.Log("Agent hit the wall! Resseting position...");
            transform.localPosition = new Vector3(0, 0, 0);
            hitWall = true;
            groundRenderer.material = hitWallMaterial;
        }
        else if (collision.gameObject.CompareTag("Target"))
        {
            Debug.Log("Agent reached the target! Reseting positions...");
            transform.localPosition = new Vector3(0, 0, 0);
            target.localPosition = new Vector3(UnityEngine.Random.Range(-13, 13), 0f, 0f);
            hitTarget = true;
            groundRenderer.material = hitTargetMaterial;
        }
    }


    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            hitWall = false;
        }
        else if (collision.gameObject.CompareTag("Target"))
        {
            hitTarget = false;
        }
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        server.Stop();
    }
}
