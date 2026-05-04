//
//  TextInputBridge.swift
//  MeloNX
//
//  Native iOS replacement for the broken RyujinxHelper.framework keyboard.
//  Exposes C symbols consumed by Ryujinx.Headless.SDL2.dylib via DllImport("__Internal").
//

import Foundation
import UIKit

private final class TextInputBridge {
    static let shared = TextInputBridge()

    enum State: Int32 {
        case pending = 0
        case accepted = 1
        case cancelled = 2
    }

    private let lock = NSLock()
    private var state: State = .pending
    private var resultBuffer: UnsafeMutablePointer<CChar>?

    func reset() {
        lock.lock()
        defer { lock.unlock() }
        state = .pending
        if let buf = resultBuffer {
            free(buf)
            resultBuffer = nil
        }
    }

    func currentState() -> Int32 {
        lock.lock()
        defer { lock.unlock() }
        return state.rawValue
    }

    func currentResultPointer() -> UnsafeMutablePointer<CChar>? {
        lock.lock()
        defer { lock.unlock() }
        return resultBuffer
    }

    private func setResult(_ text: String, accepted: Bool) {
        lock.lock()
        defer { lock.unlock() }
        if let old = resultBuffer { free(old) }
        resultBuffer = strdup(text)
        state = accepted ? .accepted : .cancelled
    }

    func presentAlert(title: String, message: String, placeholder: String) {
        DispatchQueue.main.async { [weak self] in
            guard let self else { return }
            guard let topVC = Self.topMostViewController() else {
                self.setResult("", accepted: false)
                return
            }

            let alert = UIAlertController(
                title: title.isEmpty ? "Software Keyboard" : title,
                message: message.isEmpty ? nil : message,
                preferredStyle: .alert
            )

            alert.addTextField { tf in
                tf.placeholder = placeholder
                tf.autocorrectionType = .no
                tf.autocapitalizationType = .none
                tf.clearButtonMode = .whileEditing
            }

            alert.addAction(UIAlertAction(title: "Cancel", style: .cancel) { _ in
                self.setResult("", accepted: false)
            })

            alert.addAction(UIAlertAction(title: "OK", style: .default) { _ in
                let text = alert.textFields?.first?.text ?? ""
                self.setResult(text, accepted: true)
            })

            topVC.present(alert, animated: true)
        }
    }

    private static func topMostViewController() -> UIViewController? {
        let scenes = UIApplication.shared.connectedScenes
        let windowScene = scenes.first { $0.activationState == .foregroundActive } as? UIWindowScene
            ?? scenes.first as? UIWindowScene
        guard let root = windowScene?.windows.first(where: { $0.isKeyWindow })?.rootViewController
                ?? windowScene?.windows.first?.rootViewController else { return nil }
        return topMost(of: root)
    }

    private static func topMost(of vc: UIViewController) -> UIViewController {
        if let presented = vc.presentedViewController { return topMost(of: presented) }
        if let nav = vc as? UINavigationController, let v = nav.visibleViewController { return topMost(of: v) }
        if let tab = vc as? UITabBarController, let s = tab.selectedViewController { return topMost(of: s) }
        return vc
    }
}

@_cdecl("melonx_show_text_input")
public func melonx_show_text_input(_ titlePtr: UnsafePointer<CChar>?,
                                   _ messagePtr: UnsafePointer<CChar>?,
                                   _ placeholderPtr: UnsafePointer<CChar>?) {
    let title = titlePtr.flatMap { String(validatingUTF8: $0) } ?? ""
    let message = messagePtr.flatMap { String(validatingUTF8: $0) } ?? ""
    let placeholder = placeholderPtr.flatMap { String(validatingUTF8: $0) } ?? ""
    TextInputBridge.shared.presentAlert(title: title, message: message, placeholder: placeholder)
}

@_cdecl("melonx_get_text_input_state")
public func melonx_get_text_input_state() -> Int32 {
    TextInputBridge.shared.currentState()
}

@_cdecl("melonx_get_text_input_result")
public func melonx_get_text_input_result() -> UnsafeMutablePointer<CChar>? {
    TextInputBridge.shared.currentResultPointer()
}

@_cdecl("melonx_clear_text_input")
public func melonx_clear_text_input() {
    TextInputBridge.shared.reset()
}
