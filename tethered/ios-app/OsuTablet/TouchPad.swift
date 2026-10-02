import SwiftUI

struct TouchPadView: UIViewRepresentable {
    var sender: Sender
    func makeUIView(context: Context) -> TouchView { TouchView(sender: sender) }
    func updateUIView(_ uiView: TouchView, context: Context) {}
}

class TouchView: UIView {
    let sender: Sender
    var currentTouch: CGPoint?
    var link: CADisplayLink!

    init(sender: Sender) {
        self.sender = sender
        super.init(frame: .zero)
        isUserInteractionEnabled = true
        isMultipleTouchEnabled = true
        backgroundColor = UIColor(white: 0.08, alpha: 1)
        isIdleTimerDisabled = true

        link = CADisplayLink(target: self, selector: #selector(tick))
        link.preferredFrameRateRange = CAFrameRateRange(minimum: 120, maximum: 120, preferred: 120)
        link.add(to: .main, forMode: .common)
    }
    required init?(coder: NSCoder) { fatalError() }

    @objc func tick() {
        if let p = currentTouch {
            sender.send("D \(p.x) \(p.y) 0\n")
        }
    }

    private func pos(_ t: Set<UITouch>) -> CGPoint? { t.first?.location(in: self) }

    override func touchesBegan(_ t: Set<UITouch>, with e: UIEvent?) {
        currentTouch = pos(t)
        sender.send("W \(Int(bounds.width)) \(Int(bounds.height))\n")
        if let p = currentTouch { sender.send("D \(p.x) \(p.y) 0\n") }
    }
    override func touchesMoved(_ t: Set<UITouch>, with e: UIEvent?) { currentTouch = pos(t) }
    override func touchesEnded(_ t: Set<UITouch>, with e: UIEvent?) {
        currentTouch = nil
        sender.send("U\n")
    }
    override func touchesCancelled(_ t: Set<UITouch>, with e: UIEvent?) {
        currentTouch = nil
        sender.send("U\n")
    }
}
