class GameState():
    def __init__(self, frame_manager):
        self.fm = frame_manager
    
    def GetState(self):
        raise NotImplementedError()
    
    def CollectTasks(self):
        raise NotImplementedError()
    
    def Next(self):
        raise NotImplementedError()