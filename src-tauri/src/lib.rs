// Learn more about Tauri commands at https://tauri.app/develop/calling-rust/
mod window_state;

use tauri::Manager;
use window_state::{load_window_state, save_window_state, apply_window_state};

#[tauri::command]
fn greet(name: &str) -> String {
    format!("Hello, {}! You've been greeted from Rust!", name)
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .invoke_handler(tauri::generate_handler![greet])
        .setup(|app| {
            let app_handle = app.handle();
            
            // Load saved window state
            let state = load_window_state(&app_handle);
            
            // Get the main window
            let window = app.get_window("main").expect("Failed to get main window");
            
            // Apply the saved state to the window
            apply_window_state(&window, &state);
            
            // Save window state when the window is closed
            let window_clone = window.clone();
            window.on_window_event(move |event| {
                if let tauri::WindowEvent::CloseRequested { .. } = event {
                    let _ = save_window_state(&window_clone);
                }
            });
            
            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
