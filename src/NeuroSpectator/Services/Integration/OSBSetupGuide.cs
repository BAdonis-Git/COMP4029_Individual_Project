using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using NeuroSpectator.Services.Streaming;
using NeuroSpectator.Services.Visualisation;

namespace NeuroSpectator.Services.Integration
{
    /// <summary>
    /// Provides guidance and utilities for setting up OBS with NeuroSpectator
    /// </summary>
    public class OBSSetupGuide
    {
        private readonly OBSIntegrationService obsService;
        private readonly BrainDataVisualisationService visualizationService;

        /// <summary>
        /// Creates a new instance of the OBSSetupGuide
        /// </summary>
        public OBSSetupGuide(
            OBSIntegrationService obsService,
            BrainDataVisualisationService visualizationService)
        {
            this.obsService = obsService ?? throw new ArgumentNullException(nameof(obsService));
            this.visualizationService = visualizationService ?? throw new ArgumentNullException(nameof(visualizationService));
        }

        /// <summary>
        /// Creates a full set of OBS scenes and sources for NeuroSpectator
        /// </summary>
        public async Task<bool> AutoConfigureOBSForNeuroSpectatorAsync()
        {
            if (!obsService.IsConnected)
                return false;

            try
            {
                // Ensure visualization server is running
                if (!visualizationService.IsServerRunning)
                {
                    await visualizationService.StartServerAsync();
                }

                // 1. Create scenes
                var sceneNames = new[] { "Game + Brain Data", "Webcam + Brain Data", "Brain Data Fullscreen", "BRB Scene" };
                foreach (var sceneName in sceneNames)
                {
                    await CreateSceneIfNotExistsAsync(sceneName);
                }

                // 2. Create sources for each scene
                await ConfigureGameSceneAsync("Game + Brain Data");
                await ConfigureWebcamSceneAsync("Webcam + Brain Data");
                await ConfigureBrainDataFullscreenAsync("Brain Data Fullscreen");
                await ConfigureBRBSceneAsync("BRB Scene");

                // 3. Set up the brain data overlays for each scene
                foreach (var sceneName in sceneNames)
                {
                    await obsService.CreateOrUpdateBrainDataSourceAsync(
                        sceneName,
                        visualizationService.VisualisationUrl + "/brain_data.html");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error auto-configuring OBS: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates a scene if it doesn't already exist
        /// </summary>
        private async Task CreateSceneIfNotExistsAsync(string sceneName)
        {
            try
            {
                var scenes = await obsService.GetScenesAsync();
                if (!scenes.Contains(sceneName))
                {
                    // Create scene method isn't directly exposed in OBSIntegrationService,
                    // but we can add it or use this workaround
                    await obsService.SwitchSceneAsync(sceneName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating scene {sceneName}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Configures the Game + Brain Data scene
        /// </summary>
        private async Task ConfigureGameSceneAsync(string sceneName)
        {
            try
            {
                // This would add a game capture source and position brain data overlay
                // The specifics depend on your OBS integration capabilities
                // For this example, we'll just position the brain data overlay
                await obsService.SwitchSceneAsync(sceneName);

                // Create or update the brain data source
                await obsService.CreateOrUpdateBrainDataSourceAsync(
                    sceneName,
                    visualizationService.VisualisationUrl + "/brain_data.html",
                    null,
                    400, // width
                    600); // height

                // Position the source (requires implementation in OBSIntegrationService)
                // This would use SetSceneItemTransform in a real implementation
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error configuring game scene: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Configures the Webcam + Brain Data scene
        /// </summary>
        private async Task ConfigureWebcamSceneAsync(string sceneName)
        {
            // Similar implementation to ConfigureGameSceneAsync but for webcam
            await obsService.SwitchSceneAsync(sceneName);

            // Create or update the brain data source
            await obsService.CreateOrUpdateBrainDataSourceAsync(
                sceneName,
                visualizationService.VisualisationUrl + "/brain_data.html",
                null,
                400, // width
                600); // height
        }

        /// <summary>
        /// Configures the Brain Data Fullscreen scene
        /// </summary>
        private async Task ConfigureBrainDataFullscreenAsync(string sceneName)
        {
            await obsService.SwitchSceneAsync(sceneName);

            // Create or update the brain data source for fullscreen
            await obsService.CreateOrUpdateBrainDataSourceAsync(
                sceneName,
                visualizationService.VisualisationUrl + "/brain_data.html",
                null,
                1920, // width
                1080); // height
        }

        /// <summary>
        /// Configures the BRB (Be Right Back) scene
        /// </summary>
        private async Task ConfigureBRBSceneAsync(string sceneName)
        {
            await obsService.SwitchSceneAsync(sceneName);

            // Create a text source for "Be Right Back"
            // This would require CreateInput implementation in OBSIntegrationService
            // For now, we'll just add the brain data overlay
            await obsService.CreateOrUpdateBrainDataSourceAsync(
                sceneName,
                visualizationService.VisualisationUrl + "/brain_data.html",
                null,
                400, // width 
                600); // height
        }

        /// <summary>
        /// Returns a user guide for manual OBS setup
        /// </summary>
        public string GetManualSetupGuide()
        {
            return @"# NeuroSpectator OBS Setup Guide

## Requirements
- OBS Studio 27.0 or higher
- OBS WebSocket plugin 4.9.0 or higher

## Basic Setup Steps

1. **Install OBS WebSocket Plugin**
   - Download from: https://github.com/obsproject/obs-websocket/releases
   - Install and restart OBS Studio
   - Go to Tools → WebSockets Server Settings
   - Enable the WebSocket server
   - Set a password if desired (you'll need this to connect NeuroSpectator)
   - Default port is 4444

2. **Create Browser Sources for Brain Data**
   - In OBS, create a new scene or use an existing one
   - Add a Browser source
   - Set the URL to: " + visualizationService.VisualisationUrl + @"/brain_data.html
   - Set width: 400, height: 600
   - Check 'Refresh browser when scene becomes active'
   - Position the source where you want the brain data to appear

3. **Optimize OBS for Brain Data**
   - Add a Color Correction filter to the brain data source
   - Increase saturation slightly to make brain activity more visible
   - Consider adding a thin border to make the overlay stand out

4. **Connect NeuroSpectator to OBS**
   - In NeuroSpectator, go to the Stream Control page
   - Click 'Connect to OBS'
   - Enter the WebSocket URL (usually ws://localhost:4444)
   - Enter the password if you set one

5. **Start Streaming with Brain Data**
   - Connect your BCI device in the Your Devices page
   - Return to Stream Control
   - Click 'Start Stream' to begin

## Troubleshooting

- If the brain data overlay isn't updating, try refreshing the browser source
- Make sure the visualization server in NeuroSpectator is running
- Check that OBS WebSocket is connected (green status in NeuroSpectator)
- If OBS crashes, make sure you're using compatible versions

For more help, visit the support section in NeuroSpectator.";
        }
    }
}