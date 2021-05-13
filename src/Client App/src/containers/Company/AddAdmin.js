import React from 'react';
import { Modal } from 'react-bootstrap';
import AdminForm from './AdminForm';

const AddAdmin = ({ show, onClose, data }) => {
    
    return (
        <Modal show={show} onHide={onClose} centered size="lg" >          
            <Modal.Body className="bgGradient">{<AdminForm onClose={onClose} data={data} />}</Modal.Body>
        </Modal>
    )
};

export default AddAdmin;