//
//  Switch2HomeView.swift
//  MeloNX
//
//  Switch 2 style home screen.
//

import SwiftUI

struct Switch2HomeView: View {
    @EnvironmentObject var gameHandler: LaunchGameHandler
    @EnvironmentObject var ryujinx: Ryujinx
    @StateObject var perGameSettings = PerGameSettingsManager.shared
    @StateObject var nativeSettings = NativeSettingsManager.shared
    @State private var selectedDock: DockItem = .eshop
    @State private var hoveredGame: Game?
    @State private var sheet: HomeSheet?
    @State private var showQuitAlert = false
    @State private var showCosmeticAlert = false
    @State private var cosmeticAlertTitle = ""
    @State private var now = Date()

    private var firmwareVersion: String {
        let v = ryujinx.fetchFirmwareVersion()
        return v.isEmpty ? "0" : v
    }

    private let clockTimer = Timer.publish(every: 30, on: .main, in: .common).autoconnect()

    var body: some View {
        ZStack {
            LinearGradient(
                colors: [Color(red: 0.92, green: 0.92, blue: 0.93),
                         Color(red: 0.87, green: 0.87, blue: 0.89)],
                startPoint: .top,
                endPoint: .bottom
            )
            .ignoresSafeArea()

            VStack(spacing: 0) {
                topBar
                    .padding(.horizontal, 24)
                    .padding(.top, 12)

                Spacer(minLength: 16)

                gameShelf
                    .frame(maxHeight: 320)

                Spacer(minLength: 12)

                if let g = hoveredGame ?? gameHandler.currentGame {
                    Text(g.titleName)
                        .font(.title3.weight(.medium))
                        .foregroundStyle(.primary)
                        .lineLimit(1)
                        .padding(.bottom, 4)
                } else {
                    Text(dockLabel)
                        .font(.title3.weight(.medium))
                        .foregroundStyle(Color.blue)
                        .padding(.bottom, 4)
                }

                dock
                    .padding(.horizontal, 24)
                    .padding(.bottom, 8)

                hintsRow
                    .padding(.horizontal, 24)
                    .padding(.bottom, 8)
            }
        }
        .onAppear {
            ryujinx.addGames()
        }
        .onReceive(NotificationCenter.default.publisher(for: UIApplication.willEnterForegroundNotification)) { _ in
            ryujinx.addGames()
        }
        .onReceive(clockTimer) { now = $0 }
        .sheet(item: $sheet) { which in
            switch which {
            case .settings: SettingsViewNew()
            case .account:  AccountManagerView()
            }
        }
        .alert("Quit MeloNX?", isPresented: $showQuitAlert) {
            Button("Quit", role: .destructive) {
                UIApplication.shared.perform(#selector(NSXPCConnection.suspend))
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.2) { exit(0) }
            }
            Button("Cancel", role: .cancel) {}
        }
        .alert(cosmeticAlertTitle, isPresented: $showCosmeticAlert) {
            Button("OK", role: .cancel) {}
        } message: {
            Text("This feature isn't available in MeloNX.")
        }
    }

    // MARK: - Top bar

    private var topBar: some View {
        HStack(alignment: .center) {
            Button { sheet = .account } label: {
                avatarImage
                    .frame(width: 44, height: 44)
                    .clipShape(Circle())
                    .overlay(Circle().stroke(.white, lineWidth: 2))
                    .shadow(color: .black.opacity(0.12), radius: 4, x: 0, y: 2)
            }
            .buttonStyle(.plain)

            Spacer()

            optionsMenu

            HStack(spacing: 14) {
                Text(timeString)
                    .font(.title3.weight(.medium))
                    .monospacedDigit()
                Image(systemName: "wifi")
                    .foregroundStyle(.green)
                Image(systemName: batterySymbol)
                    .foregroundStyle(.green)
            }
            .foregroundStyle(.primary)
        }
    }

    private var optionsMenu: some View {
        Menu {
            Button {
                FileImporterManager.shared.importFiles(types: [.nsp, .xci, .item]) { result in
                    ImportHandler.handleAddingGame(result: result)
                }
            } label: {
                Label("Add Game", systemImage: "plus")
            }

            Button {
                FileImporterManager.shared.importFiles(types: [.nsp, .xci, .item]) { result in
                    ImportHandler.handleRunningGame(result: result, gameHandler: gameHandler)
                }
            } label: {
                Label("Open Game", systemImage: "square.and.arrow.down")
            }

            Divider()

            if firmwareVersion == "0" {
                Button {
                    FileImporterManager.shared.importFiles(types: [.folder, .zip]) { result in
                        ImportHandler.handleFirmwareImport(result: result)
                    }
                } label: {
                    Label("Install Firmware", systemImage: "square.and.arrow.down")
                }
            } else {
                Text("Firmware: \(firmwareVersion)")
                Button {
                    let game = Game(
                        containerFolder: URL(string: "none")!,
                        fileType: .item,
                        fileURL: URL(string: "0x0100000000001009")!,
                        titleName: "Mii Maker",
                        titleId: "0",
                        developer: "Nintendo",
                        version: firmwareVersion
                    )
                    gameHandler.currentGame = game
                } label: {
                    Label("Launch Mii Maker", systemImage: "person.crop.circle")
                }
            }

            Divider()

            Button {
                openDocumentsFolder()
            } label: {
                Label("Show MeloNX Folder", systemImage: "folder")
            }

            Button {
                sheet = .account
            } label: {
                Label("Profile Manager", systemImage: "person.2")
            }
        } label: {
            Image(systemName: "plus.circle.fill")
                .font(.system(size: 28))
                .foregroundStyle(.blue)
                .padding(.trailing, 4)
        }
    }

    private var avatarImage: some View {
        Group {
            if let img = currentAvatarUIImage {
                Image(uiImage: img).resizable().scaledToFill()
            } else {
                ZStack {
                    Circle().fill(Color(red: 0.95, green: 0.85, blue: 0.7))
                    Image(systemName: "person.fill")
                        .font(.system(size: 22))
                        .foregroundStyle(.white)
                }
            }
        }
    }

    private var currentAvatarUIImage: UIImage? {
        let path = URL.documentsDirectory
            .appendingPathComponent("system")
            .appendingPathComponent("Profiles.json")
        guard let data = try? Data(contentsOf: path),
              let profiles = try? JSONDecoder().decode(Profiles.self, from: data),
              let current = profiles.profiles.first(where: { $0.user_id == profiles.last_opened }) ?? profiles.profiles.first,
              let b64 = current.image,
              let imgData = Data(base64Encoded: b64),
              let img = UIImage(data: imgData)
        else { return nil }
        return img
    }

    private var timeString: String {
        let f = DateFormatter()
        f.dateFormat = "h:mm a"
        return f.string(from: now)
    }

    private var batterySymbol: String {
        UIDevice.current.isBatteryMonitoringEnabled = true
        let lvl = UIDevice.current.batteryLevel
        switch lvl {
        case 0.75...:    return "battery.100"
        case 0.5..<0.75: return "battery.75"
        case 0.25..<0.5: return "battery.50"
        case 0.05..<0.25: return "battery.25"
        case 0..<0.05:   return "battery.0"
        default:         return "battery.100"
        }
    }

    // MARK: - Game shelf

    private var gameShelf: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 18) {
                ForEach(ryujinx.games) { game in
                    gameTile(game: game)
                }

                addGameTile

                ForEach(0..<max(0, 3 - ryujinx.games.count), id: \.self) { _ in
                    emptyTile
                }
            }
            .padding(.horizontal, 24)
            .padding(.vertical, 8)
        }
    }

    private var addGameTile: some View {
        Button {
            FileImporterManager.shared.importFiles(types: [.nsp, .xci, .item]) { result in
                ImportHandler.handleAddingGame(result: result)
            }
        } label: {
            ZStack {
                RoundedRectangle(cornerRadius: 18, style: .continuous)
                    .fill(Color.white)
                RoundedRectangle(cornerRadius: 18, style: .continuous)
                    .stroke(Color.blue.opacity(0.4), style: StrokeStyle(lineWidth: 2, dash: [8, 6]))
                VStack(spacing: 8) {
                    Image(systemName: "plus.circle.fill")
                        .font(.system(size: 56, weight: .regular))
                        .foregroundStyle(.blue)
                    Text("Add Game")
                        .font(.headline)
                        .foregroundStyle(.blue)
                }
            }
            .frame(width: 240, height: 240)
            .shadow(color: .black.opacity(0.08), radius: 6, x: 0, y: 3)
        }
        .buttonStyle(.plain)
    }

    private func gameTile(game: Game) -> some View {
        let isFocused = (hoveredGame?.id == game.id)
        return Button {
            gameHandler.currentGame = game
        } label: {
            ZStack {
                if let icon = game.icon {
                    Image(uiImage: icon)
                        .resizable()
                        .aspectRatio(contentMode: .fill)
                } else {
                    LinearGradient(colors: [.blue.opacity(0.6), .red.opacity(0.6)],
                                   startPoint: .topLeading, endPoint: .bottomTrailing)
                    Text(game.titleName.prefix(1))
                        .font(.system(size: 60, weight: .bold))
                        .foregroundStyle(.white)
                }
            }
            .frame(width: 240, height: 240)
            .clipShape(RoundedRectangle(cornerRadius: 18, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: 18, style: .continuous)
                    .stroke(isFocused ? Color.blue : .black.opacity(0.08),
                            lineWidth: isFocused ? 3 : 1)
            )
            .shadow(color: .black.opacity(0.12), radius: 8, x: 0, y: 4)
            .scaleEffect(isFocused ? 1.04 : 1.0)
            .animation(.spring(response: 0.3, dampingFraction: 0.75), value: isFocused)
        }
        .buttonStyle(.plain)
        .onHover { hovering in
            hoveredGame = hovering ? game : nil
        }
    }

    private var emptyTile: some View {
        RoundedRectangle(cornerRadius: 18, style: .continuous)
            .fill(Color.white)
            .frame(width: 240, height: 240)
            .overlay(
                RoundedRectangle(cornerRadius: 18, style: .continuous)
                    .stroke(.black.opacity(0.06), lineWidth: 1)
            )
            .shadow(color: .black.opacity(0.08), radius: 6, x: 0, y: 3)
    }

    // MARK: - Dock

    private var dockItems: [DockItem] { DockItem.allCases }

    private var dockLabel: String {
        selectedDock.label
    }

    private var dock: some View {
        HStack(spacing: 14) {
            ForEach(dockItems) { item in
                dockButton(item)
            }
        }
        .padding(.horizontal, 18)
        .padding(.vertical, 10)
        .background(
            Capsule(style: .continuous)
                .fill(Color.white)
                .shadow(color: .black.opacity(0.10), radius: 8, x: 0, y: 3)
        )
    }

    private func dockButton(_ item: DockItem) -> some View {
        let isSelected = selectedDock == item
        return Button {
            selectedDock = item
            handleDock(item)
        } label: {
            ZStack {
                Circle()
                    .fill(item.tint)
                    .frame(width: 42, height: 42)
                Image(systemName: item.symbol)
                    .font(.system(size: 18, weight: .semibold))
                    .foregroundStyle(item.iconColor)
            }
            .overlay(
                Circle()
                    .stroke(Color.blue, lineWidth: isSelected ? 3 : 0)
                    .padding(-3)
            )
        }
        .buttonStyle(.plain)
    }

    private func handleDock(_ item: DockItem) {
        switch item {
        case .settings:
            sheet = .settings
        case .controllers:
            sheet = .settings
        case .album:
            openDocumentsFolder()
        case .power:
            showQuitAlert = true
        case .online, .camera, .news, .eshop, .achievements, .mobile:
            cosmeticAlertTitle = item.label
            showCosmeticAlert = true
        }
    }

    private func openDocumentsFolder() {
        let docs = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask).first!
        var url = docs.absoluteString.replacingOccurrences(of: "file://", with: "shareddocuments://")
        if ProcessInfo.processInfo.isiOSAppOnMac { url = docs.absoluteString }
        if let u = URL(string: url), UIApplication.shared.canOpenURL(u) {
            UIApplication.shared.open(u)
        }
    }

    // MARK: - Hints

    private var hintsRow: some View {
        HStack {
            Image(systemName: "gamecontroller.fill")
                .foregroundStyle(.secondary)
            Spacer()
            HStack(spacing: 4) {
                Image(systemName: "plus.circle.fill")
                Text("Options")
            }
            .foregroundStyle(.secondary)
            HStack(spacing: 4) {
                Image(systemName: "a.circle.fill")
                Text("OK")
            }
            .foregroundStyle(.secondary)
        }
        .font(.callout)
    }
}

// MARK: - Dock item enum

private enum DockItem: String, CaseIterable, Identifiable {
    case online, camera, news, eshop, album, achievements, controllers, mobile, settings, power

    var id: String { rawValue }

    var symbol: String {
        switch self {
        case .online:       return "n.circle.fill"
        case .camera:       return "camera.fill"
        case .news:         return "newspaper.fill"
        case .eshop:        return "bag.fill"
        case .album:        return "photo.on.rectangle.angled"
        case .achievements: return "trophy.fill"
        case .controllers:  return "gamecontroller.fill"
        case .mobile:       return "iphone"
        case .settings:     return "gearshape.fill"
        case .power:        return "power"
        }
    }

    var tint: Color {
        switch self {
        case .online:       return Color.red
        case .camera:       return Color(white: 0.96)
        case .news:         return Color(white: 0.96)
        case .eshop:        return Color.orange.opacity(0.85)
        case .album:        return Color(white: 0.96)
        case .achievements: return Color(white: 0.96)
        case .controllers:  return Color(white: 0.96)
        case .mobile:       return Color(white: 0.96)
        case .settings:     return Color(white: 0.96)
        case .power:        return Color(white: 0.96)
        }
    }

    var iconColor: Color {
        switch self {
        case .online, .eshop: return .white
        default:              return Color(white: 0.25)
        }
    }

    var label: String {
        switch self {
        case .online:       return "Online"
        case .camera:       return "Camera"
        case .news:         return "News"
        case .eshop:        return "Nintendo eShop"
        case .album:        return "Album"
        case .achievements: return "Achievements"
        case .controllers:  return "Controllers"
        case .mobile:       return "Mobile"
        case .settings:     return "System Settings"
        case .power:        return "Sleep Mode"
        }
    }
}

private enum HomeSheet: String, Identifiable {
    case settings, account
    var id: String { rawValue }
}
