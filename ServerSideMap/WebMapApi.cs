using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using UnityEngine;

namespace ServerSideMap
{
    public static class WebMapApi
    {
        public static void HandleApiRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            var path = request.Url.AbsolutePath;

            try
            {
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Content-Type", "application/json");

                if (path == "/api/mapinfo")
                {
                    HandleMapInfo(response);
                }
                else if (path == "/api/explored")
                {
                    HandleExplored(response);
                }
                else if (path == "/api/pins")
                {
                    HandlePins(response);
                }
                else if (path == "/api/players")
                {
                    HandlePlayers(response);
                }
                else
                {
                    response.StatusCode = 404;
                    response.Close();
                }
            }
            catch (Exception ex)
            {
                Utility.Log($"Error handling API request {path}: {ex.Message}");
                response.StatusCode = 500;
                response.Close();
            }
        }

        private static void HandleMapInfo(HttpListenerResponse response)
        {
            var json = $"{{\"mapSize\":{ExplorationDatabase.MapSize},\"version\":3}}";
            SendJsonResponse(response, json);
        }

        private static void HandleExplored(HttpListenerResponse response)
        {
            var explored = ExplorationDatabase.GetExplorationArray();
            if (explored == null)
            {
                response.StatusCode = 500;
                response.Close();
                return;
            }

            // Compress the bool array into bytes (bit packing)
            var compressed = CompressBoolArray(explored);
            var base64 = Convert.ToBase64String(compressed);
            var json = $"{{\"data\":\"{base64}\",\"mapSize\":{ExplorationDatabase.MapSize}}}";
            SendJsonResponse(response, json);
        }

        private static void HandlePins(HttpListenerResponse response)
        {
            var pins = ExplorationDatabase.GetPins();
            var pinList = new List<string>();

            foreach (var pin in pins)
            {
                var pinJson = $"{{\"name\":\"{EscapeJson(pin.Name)}\",\"pos\":{{\"x\":{pin.Pos.x},\"y\":{pin.Pos.y},\"z\":{pin.Pos.z}}},\"type\":{(int)pin.Type},\"checked\":{(pin.Checked ? "true" : "false")}}}";
                pinList.Add(pinJson);
            }

            var json = $"[{string.Join(",", pinList)}]";
            SendJsonResponse(response, json);
        }

        private static void HandlePlayers(HttpListenerResponse response)
        {
            var players = PlayerTracker.GetPlayers();
            var playerList = new List<string>();

            foreach (var player in players)
            {
                if (!player.visible)
                    continue;

                var playerJson = $"{{\"name\":\"{EscapeJson(player.name)}\",\"pos\":{{\"x\":{player.pos.x},\"y\":{player.pos.y},\"z\":{player.pos.z}}},\"visible\":true}}";
                playerList.Add(playerJson);
            }

            var json = $"[{string.Join(",", playerList)}]";
            SendJsonResponse(response, json);
        }

        private static byte[] CompressBoolArray(bool[] array)
        {
            var bytes = new List<byte>();
            byte currentByte = 0;
            int bitIndex = 0;

            foreach (var value in array)
            {
                if (value)
                {
                    currentByte |= (byte)(1 << bitIndex);
                }

                bitIndex++;
                if (bitIndex >= 8)
                {
                    bytes.Add(currentByte);
                    currentByte = 0;
                    bitIndex = 0;
                }
            }

            if (bitIndex > 0)
            {
                bytes.Add(currentByte);
            }

            return bytes.ToArray();
        }

        private static string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str))
                return "";

            return str.Replace("\\", "\\\\")
                     .Replace("\"", "\\\"")
                     .Replace("\n", "\\n")
                     .Replace("\r", "\\r")
                     .Replace("\t", "\\t");
        }

        private static void SendJsonResponse(HttpListenerResponse response, string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.Close();
        }
    }
}

