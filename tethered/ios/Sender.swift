import Foundation
import Network

class Sender {
    private var conn: NWConnection?
    private var host = "172.20.10.2", port = 4242

    func connect(host: String, port: Int = 4242) {
        self.host = host; self.port = port
        conn?.cancel()
        conn = NWConnection(host: NWEndpoint.Host(host), port: NWEndpoint.Port(integerLiteral: UInt16(port)), using: .udp)
        conn?.start(queue: .global())
    }

    func send(_ text: String) {
        guard let data = text.data(using: .ascii), let conn else { return }
        conn.send(content: data, completion: .idempotent)
    }
}
