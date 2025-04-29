# VR Assistive System for Environmental Awareness

A VR-based assistive solution designed for visually impaired users to enhance spatial awareness through real-time room descriptions, object detection, and guided hand navigation via voice commands.

Built with Unity (C#), integrated with Google Gemini Vision API, YOLOv8 detection, Firebase Realtime Database, and Colab-hosted backend services.

---

## Key Features

- **Voice Commands Interface**:

  - "Describe the room for me"
  - "What is currently in front of me?"
  - "Guide me to the TV" (or any detected object)
  - "Stop" to cancel guidance
  - "List available commands"

- **Real-time Room Description**:

  - Analyze the environment and generate detailed natural language descriptions using AI.

- **Real-time Object Detection**:

  - Detect multiple objects in front of the user using YOLOv8 deep learning models.

- **Spatial Hand Guidance**:

  - Assist users to physically locate objects through spatialized audio cues and directional instructions.

- **Text-to-Speech (TTS)**:

  - Convert detected information and guidance into voice feedback for users.

- **Dynamic Backend Integration**:
  - Cloud-hosted backend deployed via Google Colab and Cloudflare Tunnel.
  - Real-time Firebase URL management for seamless backend updates.

---

## Project Structure

| Folder               | Description                                                                 |
| :------------------- | :-------------------------------------------------------------------------- |
| `/Assets/`           | Main Unity project files (scenes, prefabs, C# scripts)                      |
| `/Assets/Scripts/`   | Logic for AI communication, voice commands, object detection, hand guidance |
| `/Assets/Resources/` | Audio files and spatial sounds                                              |
| `/README.md`         | Project documentation                                                       |

## Backend Setup (Optional but Required for Full Functionality)

This project requires a backend server for room captioning, object detection, and text-to-speech synthesis.

1. Open a new Google Colab notebook.
2. Install the required Python libraries:
   pip install flask transformers torch firebase-admin ultralytics TTS opencv-python
3. Set the following environment variables:
   %env FIREBASE_CREDENTIALS_JSON=/path/to/your/firebase-adminsdk.json
   %env FIREBASE_DATABASE_URL=(insert your own url here)
4. Copy and run custom server script (colab_server.py):
   from flask import Flask, request, jsonify, send_file
   import torch
   import io
   import base64
   from PIL import Image
   from transformers import BlipProcessor, BlipForConditionalGeneration
   import firebase_admin
   from firebase_admin import credentials, db
   import subprocess
   import time
   import re
   import threading
   import os
   from TTS.api import TTS
   from ultralytics import YOLO  
   import cv2
   import numpy as np

# ✅ Firebase Setup

# To use please provide your personal Firebase credentials path from environment

json_path = os.environ.get('FIREBASE_CREDENTIALS_JSON')

if not firebase_admin.\_apps and json_path:
cred = credentials.Certificate(json_path)
firebase_admin.initialize_app(cred, {
"databaseURL": os.environ.get('FIREBASE_DATABASE_URL')
})

# ✅ Install Cloudflared if missing

def install_cloudflared():
subprocess.run("wget https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64 -O cloudflared", shell=True)
subprocess.run("chmod +x cloudflared", shell=True)

install_cloudflared()

def start_cloudflare_tunnel():
print("Starting Cloudflare Tunnel...")
if os.path.exists("tunnel.log"):
os.remove("tunnel.log")

    subprocess.Popen(
        "./cloudflared tunnel --url http://127.0.0.1:5000 > tunnel.log 2>&1 &",
        shell=True
    )
    time.sleep(5)

    tunnel_url = None
    retries = 20
    for _ in range(retries):
        try:
            with open("tunnel.log", "r") as log_file:
                logs = log_file.read()
                matches = re.findall(r"https://[^\s]+", logs)
                for url in matches:
                    if "trycloudflare.com" in url:
                        tunnel_url = url
                        print(f"Found Cloudflare Tunnel: {tunnel_url}")
                        return tunnel_url
        except FileNotFoundError:
            pass
        time.sleep(1)

    print("Failed to get Cloudflare Tunnel URL.")
    return None

# ✅ Flask Setup

app = Flask(**name**)

def run_flask():
app.run(host="0.0.0.0", port=5000, debug=True, use_reloader=False)

flask_thread = threading.Thread(target=run_flask)
flask_thread.start()

public_url = start_cloudflare_tunnel()

if public_url:
print(f"Cloudflare URL: {public_url}")
print(f"Captioning Endpoint: {public_url}/describe_image")
print(f"TTS Endpoint: {public_url}/text_to_speech")
print(f"YOLO Detection Endpoint: {public_url}/detect")

    try:
        ref = db.reference("/")
        ref.update({
            "cloudflare_url": public_url,
            "cloudflare_yolo_url": public_url
        })
        print("✅ Firebase updated with URLs")
    except Exception as e:
        print(f"❌ Firebase update failed: {e}")

# ✅ Load Models

caption_processor = BlipProcessor.from_pretrained("Salesforce/blip-image-captioning-large")
caption_model = BlipForConditionalGeneration.from_pretrained("Salesforce/blip-image-captioning-large").to("cuda" if torch.cuda.is_available() else "cpu")
tts_model = TTS(model_name="tts_models/en/ljspeech/tacotron2-DDC", progress_bar=False).to("cuda" if torch.cuda.is_available() else "cpu")
yolo_model = YOLO("yolov8n.pt")

# ✅ Endpoints

@app.route('/describe_image', methods=['POST'])
def describe_image():
try:
image = Image.open(io.BytesIO(request.data)).convert("RGB")
inputs = caption_processor(images=image, return_tensors="pt").to("cuda" if torch.cuda.is_available() else "cpu")
caption_ids = caption_model.generate(\*\*inputs, max_length=250, min_length=60, num_beams=5)
description = caption_processor.batch_decode(caption_ids, skip_special_tokens=True)[0]
return jsonify({"description": description})
except Exception as e:
return jsonify({"error": str(e)}), 500

@app.route('/text_to_speech', methods=['POST'])
def text_to_speech():
try:
text = request.json.get("text", "")
file_path = "/tmp/output.wav"
tts_model.tts_to_file(text=text, file_path=file_path)
return send_file(file_path, as_attachment=True, mimetype="audio/wav")
except Exception as e:
return jsonify({"error": str(e)}), 500

@app.route('/detect', methods=['POST'])
def detect():
try:
image_bytes = request.data
nparr = np.frombuffer(image_bytes, np.uint8)
img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)

        results = yolo_model(img)[0]
        detections = []

        for box in results.boxes:
            x1, y1, x2, y2 = box.xyxy[0].tolist()
            class_id = int(box.cls[0])
            class_name = yolo_model.names[class_id]
            detections.append({
                "class": class_name,
                "x1": int(x1), "y1": int(y1), "x2": int(x2), "y2": int(y2)
            })

        return jsonify({"detections": detections})
    except Exception as e:
        return jsonify({"error": str(e)}), 500

# ✅ Keep server alive

import time
while True:
time.sleep(1)

5. The server will expose the following endpoints:

- POST /describe_image
- POST /detect
- POST /text_to_speech

6. A Cloudflare Tunnel will be automatically launched to expose the server publicly.
7. URLs are automatically updated in Firebase for Unity to fetch and connect dynamically.

Controls Summary (for manual testing):

- Spacebar - Capture frame for captioning
- Y key - Run YOLO detection
- T key - Trigger hand guidance

Technologies Used:

- Unity Engine (C#)
- Google Gemini Vision API
- YOLOv8 Object Detection
- BLIP Image Captioning (Salesforce Research)
- Coqui TTS (Text-to-speech synthesis)
- Firebase Realtime Database
- Flask (Python Server)
- Cloudflare Tunnel

Important Notes:

- No API keys or Firebase credentials are stored in this repository.
- All sensitive information must be loaded dynamically via environment variables or managed secrets.
- The backend server is optional but required for full project functionality.

Future Improvements:

- Expanded object detection models.
- Additional real-time guidance features (Room navigation)
- Multi-language TTS options.

Acknowledgments:

- Salesforce Research for BLIP Image Captioning
- Ultralytics for YOLOv8 Object Detection
- Coqui TTS for Text-to-Speech synthesis
- Google Cloud for Gemini Vision API access
