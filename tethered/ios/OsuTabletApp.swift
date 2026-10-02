import SwiftUI

@main
struct OsuTabletApp: App {
    @StateObject private var vm = VM()
    var body: some Scene {
        WindowGroup {
            if vm.started {
                TouchPadView(sender: vm.sender).ignoresSafeArea()
            } else {
                VStack(spacing: 20) {
                    Text("USB Tablet").font(.title)
                    TextField("PC RNDIS IP", text: $vm.ip).textFieldStyle(.roundedBorder)
                    Button("Start") { vm.sender.connect(host: vm.ip); vm.started = true }
                        .buttonStyle(.borderedProminent)
                    Text("Windows app prints the IP — type it once, keep it for next time (set via UserDefaults).")
                        .font(.caption).multilineTextAlignment(.center)
                }.padding()
            }
        }
    }
}

class VM: ObservableObject {
    @Published var ip = "172.20.10.2"
    @Published var started = false
    let sender = Sender()
}
